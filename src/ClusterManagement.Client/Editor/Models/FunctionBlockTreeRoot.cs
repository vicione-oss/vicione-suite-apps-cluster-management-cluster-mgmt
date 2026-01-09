namespace ClusterManagement.Client.Editor.Models;
internal class FunctionBlockTreeRoot
{
    public List<FunctionBlockModel> FunctionBlocks { get; set; } = [];

    public bool? Checked => FunctionBlockModel.DetermineCheckState(FunctionBlocks);

    public string Name { get; } = "All";

    internal void ToggleExpandAll(bool value)
    {
        foreach (var fb in FunctionBlocks)
        {
            fb.IsExpanded = value;
            fb.SetExpandValueToChildren(value);
        }
    }
}
