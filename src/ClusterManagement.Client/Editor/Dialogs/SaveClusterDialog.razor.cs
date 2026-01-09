using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace ClusterManagement.Client.Editor.Dialogs;

public sealed partial class SaveClusterDialog : IDisposable
{
    private DxDialog? _refDialog;

    [Inject]
    private SaveClusterService SaveService { get; set; } = default!;

    protected override void OnInitialized()
        => SaveService.PropertyChanged += OnPropertyChanged;

    private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SaveService.DisplayDialog))
            await InvokeAsync(StateHasChanged);
        else if (args.PropertyName == nameof(SaveService.ConfirmationMessage))
            await InvokeAsync(StateHasChanged);
        else if (args.PropertyName == nameof(SaveService.IsWorking))
            await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(SaveService.DisplayDialog);

    public void Dispose()
        => SaveService.PropertyChanged -= OnPropertyChanged;
}
