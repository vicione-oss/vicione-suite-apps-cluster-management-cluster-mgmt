using ClusterManagement.Backend.DeployCluster.StateMachines;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

/// <summary>
/// The CurrentState values are persisted, so changing them breaks sagas that are already stored.
/// </summary>
public class DeployClusterStateMachineStateValueTests
{
    public static TheoryData<int, string> PersistedStates => new()
    {
        { 0, "None" },
        { 1, "Initial" },
        { 2, "Final" },
        { 3, "CommittingCluster" },
        { 4, "AddingClusterNodes" },
        { 5, "UpdatingDeployments" },
        { 6, "CleaningClusterNodes" },
        { 7, "CleaningCluster" },
        { 8, "RollingBackDeployments" },
        { 9, "RollingBackCommit" },
        { 10, "RollingBackClusterNodes" },
    };

    [Theory]
    [MemberData(nameof(PersistedStates))]
    public void GetState_should_keep_persisted_state_names(int value, string expectedName)
        => DeployClusterStateMachine.GetState(value).Should().Be(expectedName);

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void GetState_should_return_unknown_for_undefined_values(int value)
        => DeployClusterStateMachine.GetState(value).Should().Be("Unknown");

    [Theory]
    [MemberData(nameof(PersistedStates))]
    public async Task State_machine_should_map_persisted_values_to_the_same_states(int value, string expectedName)
    {
        if (value == 0)
            return; // "None" is not a state of the machine

        var machine = new DeployClusterStateMachine(Substitute.For<IServiceProvider>(), NullLogger<DeployClusterStateMachine>.Instance);
        var saga = new DeployClusterState { CurrentState = value };

        var state = await machine.GetState(saga);

        state.Name.Should().Be(expectedName);
    }
}
