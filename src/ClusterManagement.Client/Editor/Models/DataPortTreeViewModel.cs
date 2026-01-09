using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Models;

public sealed class DataPortTreeViewModel(DataPortTreeNode dataItem)
{
    public DataPortTreeNode DataItem { get; } = dataItem;
    public List<DataPortTreeViewModel> Children => [.. DataItem.Children.Select(k => new DataPortTreeViewModel(k))];
    public string Name => DataItem.Name;
    public bool HasChildren => DataItem.Children.Count > 0;
    public string? CssIcon => DataItem.Icon;
}
