namespace ClusterManagement.Client.Editor.FunctionBlockTree;

/// <summary>
/// Tree node template showing a tri-state check box in front of the icon and the text.
/// </summary>
public sealed partial class FunctionBlockCheckNode
{
    private bool? _checkState;

    private FunctionBlockTreeAdapter Adapter => (FunctionBlockTreeAdapter)Builder.Adapter;

    protected override void Calculate()
        => _checkState = Adapter.GetCheckState(Node.TreeNode);

    // an unchecked box reports the indeterminate value when clicked, that means "check"
    private void OnCheckedChanged(bool? value)
        => Adapter.SetChecked(Node.TreeNode, value ?? true);
}
