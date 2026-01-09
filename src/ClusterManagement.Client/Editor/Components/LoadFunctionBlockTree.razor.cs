using System.Text;
using ClusterManagement.Client.Editor.Models;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Editor.Components;

public sealed partial class LoadFunctionBlockTree
{
    [Parameter] public bool IsVisible { get; set; } = true;
    [Parameter] public int Layer { get; set; }

    [Parameter]
    [EditorRequired]
    public required FunctionBlockModel FunctionBlockModel { get; set; }

    [Parameter] public LoadFunctionBlockTree? Parent { get; set; }
    [Parameter] public EventCallback<bool> CheckChanged { get; set; }
    [Parameter] public required RenderFragment<FunctionBlockModel> FunctionBlockModelTemplate { get; set; }

    private bool HasSiblings => FunctionBlockModel.Parent is not null && FunctionBlockModel.Parent.FunctionBlocks.Count > 1;

    private bool IsLastSibling => FunctionBlockModel.Parent is null ||
        FunctionBlockModel.Parent.FunctionBlocks.LastOrDefault() == FunctionBlockModel;

    public bool IsFunctionBlock
        => FunctionBlockModel.FunctionBlocks.Count == 0;

    private bool IsSubLayer
        => Layer > 0;

    private string GetStateCssClasses()
    {
        var resultBuilder = new StringBuilder();

        if (IsSubLayer)
        {
            if (Layer < 2)
            {
                resultBuilder.Append("upper-layer");
                resultBuilder.Append(' ');
            }

            if (IsFunctionBlock)
                resultBuilder.Append("block-layer");
            else
                resultBuilder.Append("sub-layer");

            resultBuilder.Append(' ');
        }

        if (IsLastSibling)
        {
            resultBuilder.Append("last-sibling");
            resultBuilder.Append(' ');
        }

        if (!IsVisible)
        {
            resultBuilder.Append("hidden");
            resultBuilder.Append(' ');
        }

        return resultBuilder.ToString();
    }

    private async Task OnCheckedChanged(bool? check)
    {
        FunctionBlockModel.Checked = check;

        FunctionBlockModel.SetCheckValueToChildren(check);
        if (Parent is not null)
            await Parent.RefreshTree(true);
        else
            await RefreshTree(true);
    }

    internal async Task RefreshTree(bool invokesEvent = false)
    {
        if (Parent is null)
        {
            await InvokeAsync(StateHasChanged);

            if (invokesEvent && CheckChanged.HasDelegate)
                await CheckChanged.InvokeAsync(FunctionBlockModel.Checked ?? true);
        }
        else
        {
            await Parent.RefreshTree(invokesEvent);
        }
    }
}
