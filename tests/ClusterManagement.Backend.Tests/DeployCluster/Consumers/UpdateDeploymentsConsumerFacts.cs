using ClusterManagement.Public.ErrorHandling;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public partial class UpdateDeploymentsConsumerFacts
{
    public record class ClusterState(Existence Application, Existence Host, Activation Engine, Existence FunctionBlock);

    public record class EngineDbState(Existence Host, Activation Engine, Existence FunctionBlock);

    public record class PhysicalState(Stoppable Host, Stoppable Engine);

    public record class ExpectedState(Runnable Host, Stoppable Engine);

    public record class EngineHostActions(bool Deploy, bool AppSettings, bool Start, bool Stop, bool TearDown);

    public record class EngineActions(bool Deploy, bool Start, bool Started, bool Stop, bool Stopped, bool TearDown, bool TornDown);

    public enum Existence
    {
        None,
        Defined,
    }
    public enum Activation
    {
        None,
        Disabled,
        Enabled,
    }
    public enum Stoppable
    {
        None,
        Stopped,
        Running,
    }
    public enum Runnable
    {
        None,
        Running,
    }

    public static TheoryData<ClusterState, EngineDbState, PhysicalState, ExpectedState, EngineHostActions, EngineActions> UseCases() => new()
    {
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.None, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.None, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, false), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, false, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, false, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.None, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.None, Stoppable.None), new(false, false, false, true, true), new(false, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, true, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, true, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, true, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(true, true, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, true, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(true, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, false, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Disabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Stopped), new(false, false, false, false, false), new(false, false, false, true, true, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.None), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.None, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.None, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, false, true, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Disabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.None), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, true, true, true, true) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.None, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(true, true, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Stopped, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, true, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.None), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(true, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Stopped), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, true, true, false, false, false, false) },
        { new(Existence.Defined, Existence.Defined, Activation.Enabled, Existence.Defined), new(Existence.Defined, Activation.Enabled, Existence.Defined), new(Stoppable.Running, Stoppable.Running), new(Runnable.Running, Stoppable.Running), new(false, false, false, false, false), new(false, false, false, false, false, false, false) },
    };

    public sealed class Consume : UpdateDeploymentsConsumerFacts
    {
        private static readonly Guid s_engineHost = Guid.NewGuid();
        private static readonly Guid s_engine = Guid.NewGuid();
        private static readonly Guid s_functionBlock = Guid.NewGuid();

        [Theory]
        [MemberData(nameof(UseCases))]
        public async Task Handles_different_states_correctly(ClusterState cluster, EngineDbState engineDbContext, PhysicalState physical,
            ExpectedState _, EngineHostActions engineHost, EngineActions engine)
        {
            Arrange(cluster, engineDbContext, physical);

            await When_updating_deployments();

            await Assert(cluster, engineHost, engine);
        }

        private void Arrange(ClusterState cluster, EngineDbState engineDbContext, PhysicalState physical)
        {
            if (cluster.Application == Existence.Defined)
                Given_configured_suite();
            if (cluster.Host == Existence.Defined)
                Given_user_added_engineHost(s_engineHost);
            switch (cluster.Engine)
            {
                case Activation.Disabled:
                    Given_user_added_engine(s_engine, s_engineHost);
                    Given_user_disabled_engine(s_engine);
                    break;
                case Activation.Enabled:
                    Given_user_added_engine(s_engine, s_engineHost);
                    break;
            }
            if (cluster.FunctionBlock == Existence.Defined)
                Given_user_added_functionBlock(s_engine, s_functionBlock);

            if (engineDbContext.Host == Existence.Defined)
                Given_engineHost_is_known(s_engineHost);
            if (engineDbContext.Engine != Activation.None)
                Given_engine_is_known(s_engineHost, s_engine, shouldRun: engineDbContext.Engine == Activation.Enabled);
            if (engineDbContext.FunctionBlock == Existence.Defined)
                Given_engine_has_functionBlock(s_engine, s_functionBlock);

            switch (physical.Host)
            {
                case Stoppable.Stopped:
                    Given_engineHost_is_deployed(s_engineHost);
                    break;
                case Stoppable.Running:
                    Given_engineHost_is_deployed(s_engineHost);
                    Given_engineHost_is_started(s_engineHost);
                    break;
            }

            switch (physical.Engine)
            {
                case Stoppable.Stopped:
                    if (physical.Host == Stoppable.None)
                        throw new InvalidOperationException("Cannot deploy an engine on a non-existing engine host.");
                    Given_engine_is_deployed(s_engineHost, s_engine);
                    break;
                case Stoppable.Running:
                    if (physical.Host != Stoppable.Running)
                        throw new InvalidOperationException("Cannot run an engine on an non-running engine host.");
                    Given_engine_is_deployed(s_engineHost, s_engine);
                    Given_engine_is_started(s_engineHost, s_engine);
                    break;
            }
        }

        private async Task Assert(ClusterState clusterState, EngineHostActions engineHost, EngineActions engine)
        {
            Then_update_succeeded();

            if (clusterState.Host == Existence.Defined && clusterState.Engine != Activation.None)
                Then_engineHost_is_known(s_engineHost);
            else
                Then_no_engineHost_is_known();

            switch (clusterState.Engine)
            {
                case Activation.None: Then_no_engine_is_known(); break;
                case Activation.Disabled: Then_engine_is_known(s_engineHost, s_engine, false); break;
                case Activation.Enabled: Then_engine_is_known(s_engineHost, s_engine, true); break;
            }

            if (engineHost.Deploy)
                await Then_engineHost_gets_deployed(s_engineHost);
            else
                await Then_no_engineHost_gets_deployed();
            if (engineHost.AppSettings)
                await Then_engineHost_gets_appsettings(s_engineHost);
            else
                await Then_no_engineHost_gets_appsettings();
            if (engineHost.Start)
                await Then_engineHost_gets_started(s_engineHost);
            else
                await Then_no_engineHost_gets_started();
            if (engineHost.Stop)
                await Then_engineHost_gets_stopped(s_engineHost);
            else
                await Then_no_engineHost_gets_stopped();
            if (engineHost.TearDown)
                await Then_engineHost_gets_torn_down(s_engineHost);
            else
                await Then_no_engineHost_gets_torn_down();

            if (engine.Deploy)
                await Then_engine_gets_deployed(s_engineHost, s_engine);
            else
                await Then_no_engine_gets_deployed();
            if (engine.Start)
                await Then_engine_gets_started(s_engineHost, s_engine);
            else
                await Then_no_engine_gets_started();
            if (engine.Started)
                Then_engine_was_started(s_engine);
            else
                Then_no_engine_was_started();
            if (engine.Stop)
                await Then_engine_gets_stopped(s_engineHost, s_engine);
            else
                await Then_no_engine_gets_stopped();
            if (engine.Stopped)
                Then_engine_was_stopped(s_engine);
            else
                Then_no_engine_was_stopped();
            if (engine.TearDown)
                await Then_engine_gets_torn_down(s_engineHost, s_engine);
            else
                await Then_no_engine_gets_torn_down();
            if (engine.TornDown)
                Then_engine_was_torn_down(s_engine);
            else
                Then_no_engine_was_torn_down();
        }
    }

    /*
     * Cluster | DBContext | Physical | Description
     * ===================================================
     *         |           |          | Empty
     *         |           |    x     | Unnecessary
     *         |     x     |          | Vanished
     *         |     x     |    x     | Regular tear down
     *    x    |           |          | Regular deploy
     *    x    |           |    x     | Unexpected
     *    x    |     x     |          | Missing
     *    x    |     x     |    x     | Ready
     */

    public class Engines : UpdateDeploymentsConsumerFacts
    {
        private readonly Guid _engineHost1 = new("e1000000-0000-0000-0000-000000000000");
        private readonly Guid _engineHost2 = new("e2000000-0000-0000-0000-000000000000");
        private readonly Guid _engine1 = new("00000000-e100-0000-0000-000000000000");
        private readonly Guid _engine2 = new("00000000-e200-0000-0000-000000000000");
        private readonly Guid _engine3 = new("00000000-e300-0000-0000-000000000000");

        public class Deploy : Engines
        {
            [Fact]
            public async Task Fails_when_engineHost_cannot_be_deployed()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_deploy_engineHost_will_fail(_engineHost1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotDeployEngineHost);
                Then_no_engineHost_is_known();
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_appsettings_cannot_be_set()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_set_appsettings_will_fail(_engineHost1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotSetAppSettingsOfEngineHost);
                Then_no_engineHost_is_known();
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engineHost_cannot_be_started()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_start_engineHost_will_fail(_engineHost1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotStartEngineHost);
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engine_cannot_be_deployed()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_deploy_engine_will_fail(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotDeployEngine);
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                Then_no_engine_is_known();
                await Then_engine_gets_deployed(_engineHost1, _engine1);
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engine_cannot_be_started()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_start_engine_will_fail(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotStartEngine);
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_engine_gets_deployed(_engineHost1, _engine1);
                await Then_engine_gets_started(_engineHost1, _engine1);
                Then_no_engine_was_started();
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Deploys_and_starts_engineHost_only_once_for_several_engines()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_user_added_engine(_engine2, _engineHost1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_deployed(_engineHost1);
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_engine_gets_deployed(_engineHost1, _engine1);
                await Then_engine_gets_started(_engineHost1, _engine1);
                Then_engine_was_started(_engine1);
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_engine_gets_deployed(_engineHost1, _engine2);
                await Then_engine_gets_started(_engineHost1, _engine2);
                Then_engine_was_started(_engine2);
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task Respects_elevated_privileges_of_engine_host(bool elevatedPrivileges)
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1, elevatedPrivileges);
                Given_user_added_engine(_engine1, _engineHost1);

                await When_updating_deployments();

                Then_update_succeeded();
                await Then_engineHost_gets_started(_engineHost1, elevatedPrivileges);
            }
        }

        public class Update : Engines
        {
            [Fact]
            public async Task Updates_changed_appsettings_of_stopped_engine_host()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_has_different_appsettings(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_no_engine_gets_deployed();
                await Then_engine_gets_started(_engineHost1, _engine1);
                Then_engine_was_started(_engine1);
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Updates_changed_appsettings_of_started_engine_host()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engineHost_has_different_appsettings(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1, true);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_engineHost_gets_appsettings(_engineHost1);
                await Then_engineHost_gets_started(_engineHost1);
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_no_engine_gets_deployed();
                await Then_engine_gets_started(_engineHost1, _engine1);
                Then_engine_was_started(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_no_engineHost_gets_torn_down();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task Restarts_started_engine_host_if_elevated_privileges_changes(bool elevatedPrivileges)
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1, elevatedPrivileges);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_engineHost_is_known(_engineHost1, !elevatedPrivileges);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1, true);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1, elevatedPrivileges);
                await Then_no_engineHost_gets_deployed();
                await Then_engineHost_gets_started(_engineHost1, elevatedPrivileges);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_no_engineHost_gets_torn_down();
                Then_engine_is_known(_engineHost1, _engine1, true);
                await Then_no_engine_gets_deployed();
                await Then_engine_gets_started(_engineHost1, _engine1);
                Then_engine_was_started(_engine1);
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
            }

            [Fact]
            public async Task Moves_one_stopped_engine_from_one_running_to_other_running_host()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine2, _engineHost1);
                Given_user_disabled_engine(_engine2);
                Given_user_added_engineHost(_engineHost2);
                Given_user_added_engine(_engine3, _engineHost2);
                Given_user_disabled_engine(_engine3);
                Given_user_added_engine(_engine1, _engineHost2);
                Given_user_disabled_engine(_engine1);

                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_known(_engineHost1, _engine2);
                Given_engine_is_deployed(_engineHost1, _engine2);
                Given_engineHost_is_known(_engineHost2);
                Given_engineHost_is_deployed(_engineHost2);
                Given_engineHost_is_started(_engineHost2);
                Given_engine_is_known(_engineHost2, _engine3);
                Given_engine_is_deployed(_engineHost2, _engine3);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_not_deployed(_engineHost1);
                await Then_engineHost_gets_no_appsettings(_engineHost1);
                await Then_engineHost_gets_not_started(_engineHost1);
                Then_engineHost_is_known(_engineHost2);
                await Then_engineHost_gets_not_deployed(_engineHost2);
                await Then_engineHost_gets_no_appsettings(_engineHost2);
                await Then_engineHost_gets_not_started(_engineHost2);
                await Then_no_engine_gets_stopped();
                Then_no_engine_was_stopped();
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                Then_engine_is_known(_engineHost2, _engine1);
                await Then_engine_gets_deployed(_engineHost2, _engine1);
                await Then_engine_gets_not_started(_engine1);
                Then_engine_was_not_started(_engine1);
                await Then_engine_gets_not_torn_down(_engine2);
                Then_engine_was_not_torn_down(_engine2);
                Then_engine_is_known(_engineHost1, _engine2);
                await Then_engine_gets_not_deployed(_engine2);
                await Then_engine_gets_not_started(_engine2);
                Then_engine_was_not_started(_engine2);
                await Then_engine_gets_not_torn_down(_engine3);
                Then_engine_was_not_torn_down(_engine3);
                Then_engine_is_known(_engineHost2, _engine3);
                await Then_engine_gets_not_deployed(_engine3);
                await Then_engine_gets_not_started(_engine3);
                Then_engine_was_not_started(_engine3);
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Moves_one_running_engine_from_one_running_to_other_running_host()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine2, _engineHost1);
                Given_user_added_engineHost(_engineHost2);
                Given_user_added_engine(_engine3, _engineHost2);
                Given_user_added_engine(_engine1, _engineHost2);

                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1, true);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_engine_is_known(_engineHost1, _engine2, true);
                Given_engine_is_deployed(_engineHost1, _engine2);
                Given_engine_is_started(_engineHost1, _engine2);
                Given_engineHost_is_known(_engineHost2);
                Given_engineHost_is_deployed(_engineHost2);
                Given_engineHost_is_started(_engineHost2);
                Given_engine_is_known(_engineHost2, _engine3, true);
                Given_engine_is_deployed(_engineHost2, _engine3);
                Given_engine_is_started(_engineHost2, _engine3);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_engineHost_gets_not_deployed(_engineHost1);
                await Then_engineHost_gets_no_appsettings(_engineHost1);
                await Then_engineHost_gets_not_started(_engineHost1);
                Then_engineHost_is_known(_engineHost2);
                await Then_engineHost_gets_not_deployed(_engineHost2);
                await Then_engineHost_gets_no_appsettings(_engineHost2);
                await Then_engineHost_gets_not_started(_engineHost2);
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                Then_engine_is_known(_engineHost2, _engine1, true);
                await Then_engine_gets_deployed(_engineHost2, _engine1);
                await Then_engine_gets_started(_engineHost2, _engine1);
                Then_engine_was_started(_engine1);
                await Then_engine_gets_not_stopped(_engine2);
                Then_engine_was_not_stopped(_engine2);
                await Then_engine_gets_not_torn_down(_engine2);
                Then_engine_was_not_torn_down(_engine2);
                Then_engine_is_known(_engineHost1, _engine2, true);
                await Then_engine_gets_not_deployed(_engine2);
                await Then_engine_gets_not_started(_engine2);
                Then_engine_was_not_started(_engine2);
                await Then_engine_gets_not_stopped(_engine3);
                Then_engine_was_not_stopped(_engine3);
                await Then_engine_gets_not_torn_down(_engine3);
                Then_engine_was_not_torn_down(_engine3);
                Then_engine_is_known(_engineHost2, _engine3, true);
                await Then_engine_gets_not_deployed(_engine3);
                await Then_engine_gets_not_started(_engine3);
                Then_engine_was_not_started(_engine3);
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Tears_down_only_single_unexpected_stopped_engine()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_user_disabled_engine(_engine1);
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1, true);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine2);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_started();
                await Then_no_engine_gets_stopped();
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine2);
                Then_no_engine_was_torn_down();
                Then_engine_is_known(_engineHost1, _engine1);
                Then_engine_is_not_known(_engineHost1, _engine2);
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Tears_down_only_single_unexpected_started_engine()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine1, _engineHost1);
                Given_user_disabled_engine(_engine1);
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine2);
                Given_engine_is_started(_engineHost1, _engine2);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_started();
                await Then_engine_gets_stopped(_engineHost1, _engine2);
                Then_no_engine_was_stopped();
                await Then_engine_gets_torn_down(_engineHost1, _engine2);
                Then_no_engine_was_torn_down();
                Then_engine_is_known(_engineHost1, _engine1);
                Then_engine_is_not_known(_engineHost1, _engine2);
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }
        }

        public class TearDown : Engines
        {
            [Fact]
            public async Task Fails_when_engine_cannot_be_stopped()
            {
                Given_configured_suite();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_stop_engine_will_fail(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotStopEngine);
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_engine_is_known(_engineHost1, _engine1);
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_no_engine_was_stopped();
                await Then_no_engine_gets_torn_down();
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engine_cannot_be_torn_down()
            {
                Given_configured_suite();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_tear_down_engine_will_fail(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotTearDownEngine);
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_no_engine_was_torn_down();
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engineHost_cannot_be_stopped()
            {
                Given_configured_suite();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_stop_engineHost_will_fail(_engineHost1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotStopEngineHost);
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Fails_when_engineHost_cannot_be_torn_down()
            {
                Given_configured_suite();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_tear_down_engineHost_will_fail(_engineHost1);

                await When_updating_deployments();

                Then_update_failed(ClusterErrors.CouldNotTearDownEngineHost);
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_engineHost_gets_torn_down(_engineHost1);
            }

            [Fact]
            public async Task Does_not_tear_down_engineHost_if_used_by_other_engine()
            {
                Given_configured_suite();
                Given_user_added_engineHost(_engineHost1);
                Given_user_added_engine(_engine2, _engineHost1);
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);
                Given_engine_is_known(_engineHost1, _engine2);
                Given_engine_is_deployed(_engineHost1, _engine2);
                Given_engine_is_started(_engineHost1, _engine2);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_engineHost_is_known(_engineHost1);
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_engine_is_known(_engineHost1, _engine2, true);
                Then_engine_is_not_known(_engineHost1, _engine1);
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engine_gets_not_stopped(_engine2);
                Then_engine_was_not_stopped(_engine2);
                await Then_engine_gets_not_torn_down(_engine2);
                Then_engine_was_not_torn_down(_engine2);
                await Then_no_engineHost_gets_stopped();
                await Then_no_engineHost_gets_torn_down();
            }

            [Fact]
            public async Task Tears_down_everything_if_cluster_application_is_removed()
            {
                Given_cluster_node_group();
                Given_cluster_node();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_no_engineHost_is_known();
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_engineHost_gets_torn_down(_engineHost1);
            }

            [Fact]
            public async Task Tears_down_everything_if_cluster_node_is_removed()
            {
                Given_cluster_node_group();
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_no_engineHost_is_known();
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_engineHost_gets_torn_down(_engineHost1);
            }

            [Fact]
            public async Task Tears_down_everything_if_cluster_node_group_is_removed()
            {
                Given_engineHost_is_known(_engineHost1);
                Given_engineHost_is_deployed(_engineHost1);
                Given_engineHost_is_started(_engineHost1);
                Given_engine_is_known(_engineHost1, _engine1);
                Given_engine_is_deployed(_engineHost1, _engine1);
                Given_engine_is_started(_engineHost1, _engine1);

                await When_updating_deployments();

                Then_update_succeeded();
                Then_no_engineHost_is_known();
                await Then_no_engineHost_gets_deployed();
                await Then_no_engineHost_gets_appsettings();
                await Then_no_engineHost_gets_started();
                Then_no_engine_is_known();
                await Then_no_engine_gets_deployed();
                await Then_no_engine_gets_started();
                Then_no_engine_was_started();
                await Then_engine_gets_stopped(_engineHost1, _engine1);
                Then_engine_was_stopped(_engine1);
                await Then_engine_gets_torn_down(_engineHost1, _engine1);
                Then_engine_was_torn_down(_engine1);
                await Then_engineHost_gets_stopped(_engineHost1);
                await Then_engineHost_gets_torn_down(_engineHost1);
            }
        }
    }
}
