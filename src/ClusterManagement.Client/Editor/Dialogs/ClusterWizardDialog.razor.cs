using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using DevExpress.Data.Helpers;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace ClusterManagement.Client.Editor.Dialogs;

public sealed partial class ClusterWizardDialog : IDisposable
{
    internal const string GenerateButtonId = "ClusterWizardGenerate";
    internal const string CancelButtonId = "ClusterWizardCancel";

    private DxDialog? _refDialog;
    private DynamicComponent? _wizardComponent;

    [Inject]
    private ClusterWizardDialogService WizardService { get; set; } = default!;

    protected override void OnInitialized() => WizardService.PropertyChanged += OnPropertyChanged;

    private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ClusterWizardDialogService.DisplayDialog))
        {
            if (WizardService.CurrentWizard is not null && WizardService.DisplayDialog)
            {
                (_wizardComponent?.Instance as IRefreshable)?.Refresh();
            }

            await InvokeAsync(StateHasChanged);
        }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(WizardService.DisplayDialog);

    public void Dispose() => WizardService.PropertyChanged -= OnPropertyChanged;
}
