using ClusterManagement.Backend.DeployCluster.StateMachines;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    /// <summary>
    /// A cancelled message faults instead of skipping the rest of the rollback, see <see cref="DeployClusterStateMachine.IsCancellation"/>.
    /// The test harness cannot cancel a message, so the check is tested on its own.
    /// </summary>
    public class Cancellation
    {
        [Fact]
        public void A_cancelled_message_is_a_cancellation()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            DeployClusterStateMachine.IsCancellation(new OperationCanceledException(cancellation.Token), cancellation.Token).Should().BeTrue();
        }

        [Fact]
        public void A_cancelled_operation_of_a_message_that_was_not_cancelled_is_a_failed_step()
        {
            // e.g. a timeout of a request, which the rollback skips like any other failure
            using var cancellation = new CancellationTokenSource();

            DeployClusterStateMachine.IsCancellation(new TaskCanceledException(), cancellation.Token).Should().BeFalse();
        }

        [Fact]
        public void Another_exception_of_a_cancelled_message_is_a_failed_step()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            DeployClusterStateMachine.IsCancellation(new InvalidOperationException("Load failed"), cancellation.Token).Should().BeFalse();
        }
    }
}
