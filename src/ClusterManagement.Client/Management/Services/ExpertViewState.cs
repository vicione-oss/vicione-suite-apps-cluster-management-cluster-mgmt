namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// Whether the Cluster Manager pages show the Expert view: engine hosts, cycle times, ids, packages and the deployment
/// state. It changes what is shown, never what the user may do.
/// </summary>
/// <remarks>
/// Kept for the circuit, so it lasts while the user moves between the Cluster Manager pages and starts in the Standard
/// view after a reload.
/// </remarks>
internal sealed class ExpertViewState
{
    public bool IsExpert { get; private set; }

    public event Action? Changed;

    public void Toggle()
    {
        IsExpert = !IsExpert;
        Changed?.Invoke();
    }
}
