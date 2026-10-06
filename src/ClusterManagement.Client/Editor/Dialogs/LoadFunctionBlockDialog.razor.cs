using System.ComponentModel;
using System.Globalization;
using ClusterManagement.Client.Editor.Components;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Editor.Dialogs;

public sealed partial class LoadFunctionBlockDialog : IDisposable
{
    private readonly string _localizeCollapseAllGroups = string.Format(CultureInfo.CurrentCulture, UserActions.CollapseSomething, CommonVocabulary.All);
    private readonly string _localizeExpandAllGroups = string.Format(CultureInfo.CurrentCulture, UserActions.ExpandSomething, CommonVocabulary.All);

    private Dialog? _refDialog;
    private FunctionBlockSelection? _selection;
    private string? _searchText;

    // kept in a field, a new instance on every render would rebuild the tree each time
    private IReadOnlyList<ClusterDependency> _availableDependencies = [];
    private IReadOnlyList<ClusterDependency> _checkedDependencies = [];

    [Inject]
    private LoadFunctionBlocksService LoadService { get; set; } = default!;

    [Inject]
    private ILogger<LoadFunctionBlockDialog> Logger { get; set; } = default!;

    protected override void OnInitialized()
        => LoadService.PropertyChanged += OnPropertyChanged;

    private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LoadService.DisplayDialog))
            await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(LoadService.DisplayDialog);

    // the popup drops its content when closed, so the selection starts unchecked and unfiltered every time it is shown
    private void OnShowing()
    {
        // already loaded function blocks are not offered again
        _availableDependencies = [.. (LoadService.Dependencies ?? []).Except(LoadService.SelectedDependencies ?? [])];
        _checkedDependencies = [];
        _searchText = string.Empty;
    }

    private void OnCheckedDependenciesChanged(IReadOnlyList<ClusterDependency> checkedDependencies)
        => _checkedDependencies = checkedDependencies;

    private void CloseDialog()
        => LoadService.DisplayDialog = false;

    private async Task OnOkButtonClick()
    {
        try
        {
            if (_checkedDependencies.Count == 0)
                return;

            LoadService.SelectedDependencies = [.. LoadService.SelectedDependencies ?? [], .. _checkedDependencies];

            await LoadService.LoadSelectedFunctionBlocks();
        }
        catch (Exception e)
        {
            LogLoadingFbDesignError(Logger, e);
        }
        finally
        {
            LoadService.DisplayDialog = false;
        }
    }

    private void SearchTextChanging(string? searchText) => _searchText = searchText;

    public void Dispose()
        => LoadService.PropertyChanged -= OnPropertyChanged;

    [LoggerMessage(Level = LogLevel.Error, Message = "Error occured while loading the FunctionBlock designs.")]
    private static partial void LogLoadingFbDesignError(ILogger<LoadFunctionBlockDialog> logger, Exception exception);
}
