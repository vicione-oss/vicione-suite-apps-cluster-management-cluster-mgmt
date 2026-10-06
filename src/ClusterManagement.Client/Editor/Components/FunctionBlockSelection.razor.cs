using ClusterManagement.Client.Editor.FunctionBlockTree;
using Microsoft.AspNetCore.Components;
using ViciOne.Cluster.Model;
using ViciOne.Ui.TreeEditor.Builder;

namespace ClusterManagement.Client.Editor.Components;

/// <summary>
/// Lets the user check function block dependencies in a tree built from their dotted names.
/// </summary>
public sealed partial class FunctionBlockSelection : IDisposable
{
    private readonly TreeBuilder _treeBuilder = new();

    private IEnumerable<ClusterDependency>? _appliedDependencies;
    private string? _appliedFilter;

    [Inject] private FunctionBlockTreeAdapter Adapter { get; set; } = default!;

    /// <summary>
    /// The dependencies offered for selection.
    /// </summary>
    [Parameter, EditorRequired] public IEnumerable<ClusterDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Only dependencies whose name contains this text are shown.
    /// </summary>
    [Parameter] public string? Filter { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<ClusterDependency>> CheckedDependenciesChanged { get; set; }

    public IReadOnlyList<ClusterDependency> CheckedDependencies => Adapter.CheckedDependencies;

    protected override async Task OnInitializedAsync()
    {
        // everything before the await runs synchronously: a re-render of the parent calls OnParametersSet
        // while the icons are still loading, the first OnParametersSet only runs after they are loaded
        _treeBuilder.SetAdapter(Adapter);
        Adapter.CheckedChanged += OnAdapterCheckedChanged;
        ApplyParameters();

        // the adapter updates the icons of the already displayed nodes once they are loaded
        await Adapter.InitializeAsync();
    }

    protected override void OnParametersSet()
        => ApplyParameters();

    private void ApplyParameters()
    {
        if (!ReferenceEquals(_appliedDependencies, Dependencies))
        {
            _appliedDependencies = Dependencies;
            Adapter.SetDependencies(Dependencies);
        }

        if (_appliedFilter != Filter)
        {
            _appliedFilter = Filter;
            Adapter.SetFilter(Filter);
        }
    }

    public void ExpandAll()
        => Adapter.ExpandAll();

    public void CollapseAll()
        => Adapter.CollapseAll();

    private async void OnAdapterCheckedChanged()
    {
        await InvokeAsync(StateHasChanged);

        if (CheckedDependenciesChanged.HasDelegate)
            await InvokeAsync(() => CheckedDependenciesChanged.InvokeAsync(Adapter.CheckedDependencies));
    }

    public void Dispose()
    {
        Adapter.CheckedChanged -= OnAdapterCheckedChanged;
        _treeBuilder.Dispose();
    }
}
