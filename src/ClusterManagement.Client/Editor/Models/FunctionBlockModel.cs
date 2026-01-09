using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Models;

public class FunctionBlockModel
{
    private bool? _checked = false;

    public string Name { get; set; } = string.Empty;
    public bool? Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            CheckParent();

            FunctionBlockCheckChanged?.Invoke(this);
        }
    }

    public bool IsExpanded { get; set; } = true;

    public ClusterDependency? Dependency { get; set; }
    public List<FunctionBlockModel> FunctionBlocks { get; } = [];
    public FunctionBlockModel? Parent { get; init; }

    public static event Action<FunctionBlockModel>? FunctionBlockCheckChanged;

    internal void CheckParent()
    {
        if (Parent is null || Parent.FunctionBlocks.Count == 0)
            return;

        Parent.Checked = Parent.FunctionBlocks.All(fb => fb.Checked == true)
            ? true
            : Parent.FunctionBlocks.All(fb => fb.Checked == false) ? false : null;
    }

    internal void SetCheckValueToChildren(bool? checkValue)
    {
        if (FunctionBlocks.Count == 0)
            return;

        foreach (var fb in FunctionBlocks)
        {
            fb.Checked = checkValue;
            fb.SetCheckValueToChildren(checkValue);
        }
    }

    internal void SetExpandValueToChildren(bool expandValue)
    {
        foreach (var fb in FunctionBlocks)
        {
            fb.IsExpanded = expandValue;
            fb.SetExpandValueToChildren(expandValue);
        }
    }

    internal void ToggleIsExpanded() => IsExpanded = !IsExpanded;

    internal IList<ClusterDependency> GetCurrentDependencies(List<ClusterDependency>? clusterDependencies = null)
    {
        clusterDependencies ??= [];

        if (Dependency is not null && Checked == true)
            clusterDependencies.Add(Dependency);

        foreach (var block in FunctionBlocks)
        {
            block.GetCurrentDependencies(clusterDependencies);
        }

        return clusterDependencies;
    }

    internal static bool? DetermineCheckState(List<FunctionBlockModel> functionBlocks)
    {
        if (functionBlocks.Count == 0)
            return false;
        else if (functionBlocks.All(fb => fb.Checked == true))
            return true;
        else if (functionBlocks.All(fb => fb.Checked == false))
            return false;
        else
            return null;
    }

    internal void RefreshParentCheckStates()
    {
        foreach (var block in FunctionBlocks)
        {
            if (block.FunctionBlocks.Count > 0)
                block.Checked = DetermineCheckState(block.FunctionBlocks);
        }
    }
}
