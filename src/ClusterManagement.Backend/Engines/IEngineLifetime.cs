namespace ClusterManagement.Backend.Engines;

internal interface IEngineLifetime
{
    Task Start(CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);
}
