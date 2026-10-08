using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using Microsoft.AspNetCore.Components;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class PackagesPanel
{
    private const int CollapsedCount = 8;

    private bool _showAll;

    [Parameter, EditorRequired] public Version Version { get; set; } = default!;
    [Parameter, EditorRequired] public IReadOnlyList<ClusterDependency> Dependencies { get; set; } = [];

    private string Title => string.Format(CultureInfo.CurrentCulture, ClusterOverview.PackagesOfVersion, Version);

    private IEnumerable<ClusterDependency> VisibleDependencies
    {
        get
        {
            var sorted = Dependencies.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
            return _showAll ? sorted : sorted.Take(CollapsedCount);
        }
    }
}
