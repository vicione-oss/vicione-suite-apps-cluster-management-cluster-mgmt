using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using Microsoft.Extensions.Logging.Abstractions;
using Step = ClusterManagement.Public.Enums.ClusterNodeDeploymentStep;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

public sealed partial class UpdateDeploymentsConsumer
{
    /// <summary>
    /// The steps the update expects to take, predicted with the decisions that run it. What the update has not found out
    /// yet is predicted from the database: a running engine host keeps the engines running that should run, and an engine
    /// host the update starts has started none. Each settings comparison and engine state query the update makes replaces
    /// a prediction, and the plan is published again when that changes it.
    /// </summary>
    private sealed class DeploymentPlan(
        IReadOnlyList<IEngineHostAction> engineHostActions,
        IReadOnlyCollection<EngineHosts.EngineHost> expectedEngineHosts,
        IReadOnlyCollection<Guid> expectedRetainMessages,
        DeploymentStepReporter steps)
    {
        private readonly Dictionary<Guid, bool> _settingsChanged = [];
        private readonly Dictionary<Guid, Dictionary<Guid, EngineState>> _engineStates = [];

        public Task Publish()
            => steps.Plan(Steps());

        public Task LearnSettingsChanged(Guid engineHostId, bool changed)
        {
            _settingsChanged[engineHostId] = changed;
            return Publish();
        }

        public Task LearnEngineStates(Guid engineHostId, IReadOnlyDictionary<Guid, EngineState> states)
        {
            _engineStates[engineHostId] = new(states);
            return Publish();
        }

        private List<DeploymentStepTarget> Steps()
        {
            List<DeploymentStepTarget> planned = [new(Step.Prepare), new(Step.Plan)];
            List<DeploymentActions> engineActions = [];
            foreach (var engineHostAction in engineHostActions)
            {
                switch (engineHostAction)
                {
                    case EngineHostProvisioning provisioning: engineActions.Add(PlanProvisioning(provisioning, planned)); break;
                    case EngineHostDisposal disposal: engineActions.Add(PlanDisposal(disposal, planned)); break;
                }
            }

            if (DeletesRetainedMessages(engineActions))
                planned.Add(new(Step.DeleteRetainedMessages));
            return planned;
        }

        // Mirrors ProvisionEngineHost and UpdateDeployments.
        private DeploymentActions PlanProvisioning(EngineHostProvisioning provisioning, List<DeploymentStepTarget> planned)
        {
            var id = provisioning.Desired.Id;
            var restarts = true;
            switch (provisioning.PhysicalState)
            {
                case EngineHostState.NotFound:
                    planned.Add(new(Step.DeployEngineHost, id));
                    planned.Add(new(Step.StartEngineHost, id));
                    break;
                case EngineHostState.Started:
                    planned.Add(new(Step.CompareEngineHostSettings, id));
                    var settingsChanged = _settingsChanged.GetValueOrDefault(id);
                    restarts = settingsChanged || provisioning.PrivilegesChanged;
                    if (restarts)
                    {
                        planned.Add(new(Step.StopEngineHost, id));
                        if (settingsChanged)
                            planned.Add(new(Step.UpdateEngineHost, id));
                        planned.Add(new(Step.StartEngineHost, id));
                    }
                    break;
                case EngineHostState.Stopped:
                    planned.Add(new(Step.CompareEngineHostSettings, id));
                    if (_settingsChanged.GetValueOrDefault(id))
                        planned.Add(new(Step.UpdateEngineHost, id));
                    planned.Add(new(Step.StartEngineHost, id));
                    break;
            }

            planned.Add(new(Step.QueryEngineStates, id));
            var states = KnownOrPredictedStates(id, provisioning.Expected, provisioning.PhysicalState, restarts);
            var actions = IdentifyDeploymentActions(provisioning, states, NullLogger<UpdateDeploymentsConsumer>.Instance);
            PlanDeployments(id, actions, planned);
            return actions;
        }

        // Mirrors DisposeEngineHost.
        private DeploymentActions PlanDisposal(EngineHostDisposal disposal, List<DeploymentStepTarget> planned)
        {
            var id = disposal.Expected.Id;
            var started = disposal.PhysicalState == EngineHostState.Started;
            if (started)
                planned.Add(new(Step.QueryEngineStates, id));

            var states = started ? KnownOrPredictedStates(id, disposal.Expected, disposal.PhysicalState, restarts: false) : [];
            var actions = IdentifyDeploymentActions(disposal, states);
            PlanDeployments(id, actions, planned);

            if (started)
                planned.Add(new(Step.StopEngineHost, id));
            if (disposal.PhysicalState != EngineHostState.NotFound)
                planned.Add(new(Step.TearDownEngineHost, id));
            return actions;
        }

        private Dictionary<Guid, EngineState> KnownOrPredictedStates(Guid engineHostId, EngineHosts.EngineHost? expected, EngineHostState physicalState,
            bool restarts)
        {
            if (_engineStates.TryGetValue(engineHostId, out var known))
                return new(known);

            if (expected is null || physicalState == EngineHostState.NotFound)
                return [];

            return expected.Deployments.ToDictionary(
                d => d.Id,
                d => d.ShouldRun && !restarts ? EngineState.Running : EngineState.Stopped);
        }

        // Mirrors UpdateDeployments: the engines it deploys and are enabled start after the ones that only start.
        private static void PlanDeployments(Guid engineHostId, DeploymentActions actions, List<DeploymentStepTarget> planned)
        {
            planned.AddRange(actions.Stop.Where(s => s.Execute).Select(s => new DeploymentStepTarget(Step.StopEngine, engineHostId, s.EngineId)));
            planned.AddRange(actions.TearDown.Where(t => t.Execute).Select(t => new DeploymentStepTarget(Step.TearDownEngine, engineHostId, t.EngineId)));
            planned.AddRange(actions.Deploy.Select(d => new DeploymentStepTarget(Step.DeployEngine, engineHostId, d.Engine.Id)));
            planned.AddRange(actions.Start.Select(s => new DeploymentStepTarget(Step.StartEngine, engineHostId, s.EngineId)));
            planned.AddRange(actions.Deploy.Where(d => d.Engine.Enabled).Select(d => new DeploymentStepTarget(Step.StartEngine, engineHostId, d.Engine.Id)));
        }

        // Mirrors DeleteRetainMessages: the deployments left afterwards are the known ones the update does not delete and
        // the ones it deploys.
        private bool DeletesRetainedMessages(List<DeploymentActions> engineActions)
        {
            var deleted = engineActions.SelectMany(a => a.Delete).Select(d => d.Id).ToHashSet();
            var kept = expectedEngineHosts.SelectMany(h => h.Deployments)
                .Where(d => !deleted.Contains(d.Id))
                .SelectMany(d => d.MqttRetainTopics);
            var deployed = engineActions.SelectMany(a => a.Deploy).SelectMany(d => d.WorkflowMetadata.MqttRetainTopics);
            return expectedRetainMessages.Except(kept.Concat(deployed)).Any();
        }
    }
}
