using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Extensions;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Ui.ClusterEditor.Services;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Editor;

public sealed partial class ClusterEditorPage
{
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private bool _resourcesLoaded;
    private bool _hasUnsavedChanges;
    private ClusterBuilder? _builder;

    [Inject] private LoadClusterService LoadService { get; set; } = default!;

    [Inject] private LoadFunctionBlocksService LoadFunctionBlockService { get; set; } = default!;

    [Inject] private IUiMediator Mediator { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private SaveClusterService SaveService { get; set; } = default!;

    [Inject] private ExportClusterService ExportService { get; set; } = default!;

    [Inject] private IClusterEditorManagement ClusterEditorManagement { get; set; } = default!;

    [Inject] private ClusterWizardDialogService WizardDialogService { get; set; } = default!;

    [Inject] private ISettingsService SettingsService { get; set; } = default!;

    [Inject] private IResourceDownloadStateService ResourceDownloadState { get; set; } = default!;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthState { get; set; }

    protected override List<Resource> Resources => new List<Resource>()
        .AddClusterEditorResources();

    protected override string PageTitle => Localization.ClusterEditor.ModuleTitle;

    protected override async ValueTask DisposeInternal()
    {
        _builder?.Dispose();
        _subscriptionHandles.Dispose();

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        LoadService.ClusterLoaded -= LoadService_ClusterLoaded;
        SaveService.ClusterSynchronized -= SaveService_ClusterSynchronized;

        ClusterEditorManagement.ExportRequested -= Editor_ExportRequested;
        ClusterEditorManagement.ImportRequested -= Editor_ImportRequested;
        ClusterEditorManagement.SaveRequested -= SaveService.SaveRequested;
        ClusterEditorManagement.LoadFunctionBlockDesignsRequested -= Editor_LoadFunctionBlockDesignsRequested;

        LayoutService.IsLoadingOverlayVisible = false;

        await base.DisposeInternal();
    }

    protected override async Task OnInitializedAsync()
    {
        LayoutService.IsLoadingOverlayVisible = true;

        var username = (await AuthState).User.Identity?.Name ?? "Anonymous";
        var request = new GetClusterEditorSettingsRequest(username);
        var response = await Mediator.Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(request, _tokenSource.Token);

        var settings = response.RequestError is not null ? new EditorSettings() { UserId = username } : response.Settings!;

        SettingsService.SetGridMode(settings.GetGridMode());
        SettingsService.SetMinimapNodeColoring(settings.MinimapNodeColoring);
        SettingsService.SetNodeAlignmentBorder(settings.NodeAlignmentBorder);
        SettingsService.SetPanBehavior(settings.PanBehavior);
        SettingsService.SetSimplifiedView(settings.SimplifiedView);

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterResourcesLoadedAsync()
    {
        // editor needs to be rendered after the js scripts were loaded!
        _resourcesLoaded = true;
        await InvokeAsync(StateHasChanged);

        // Wait for backend resources before enabling the editor
        await ResourceDownloadState.WaitForCompletion(_tokenSource.Token);

        // connect editor events to our services
        LoadService.ClusterLoaded += LoadService_ClusterLoaded;

        // will only be triggered for save executed by ourselves
        SaveService.ClusterSynchronized += SaveService_ClusterSynchronized;

        ClusterEditorManagement.ExportRequested += Editor_ExportRequested;
        ClusterEditorManagement.ImportRequested += Editor_ImportRequested;
        ClusterEditorManagement.SaveRequested += SaveService.SaveRequested;
        ClusterEditorManagement.LoadFunctionBlockDesignsRequested += Editor_LoadFunctionBlockDesignsRequested;

        await LoadService.TryLoadLatestVersion();

        LayoutService.IsLoadingOverlayVisible = false;
    }

    private async Task Editor_ExportRequested()
        => await ExportService.DownloadClusterJson(_builder?.Cluster);

    private async Task Editor_ImportRequested()
        => await WizardDialogService.OpenDialog<ImportWizardService>(_builder);

    // TODO: we need a better way to handle external changes. Currently we just reload the latest version on any change, but this can lead to data loss if there are unsaved changes.
    // We should at least show a confirmation dialog if there are unsaved changes and only reload if the user confirms.
    //private async void SaveService_ClusterChanged(object? sender, ClusterChanged change)
    //{
    //    LogLoadTriggered(Logger, change.Action);

    //    await LoadService.TryLoadLatestVersion();
    //}

    private void SaveService_ClusterSynchronized() => _hasUnsavedChanges = false;

    private void LoadService_ClusterLoaded(object? _, ClusterBuilder builder)
    {
        if (_builder is not null)
        {
            builder.ClusterChanged -= Builder_ClusterChanged;
            builder.Editors.Dataflow.DataPortAdded -= DataPortAdded;
        }

        _builder = builder;
        _hasUnsavedChanges = false;
        builder.ClusterChanged += Builder_ClusterChanged;
        builder.Editors.Dataflow.DataPortAdded += DataPortAdded;
    }

    private void DataPortAdded(object? sender, DataPort dataPort)
    {
        if (_builder == null || dataPort.EngineId != null)
            return;

        var application = _builder.Cache.ClusterApplications.FirstOrDefault();
        if (application == null)
            return;

        // take the first available engine host or create a new one
        var engineHost = _builder.Cache.EngineHosts.FirstOrDefault() ??
            _builder.GetOrCreateEngineHostByName(application, nameof(EngineHost));

        var engine = engineHost.Engines.FirstOrDefault(k => k.Name.StartsWith(dataPort.Name, StringComparison.Ordinal)) ??
            _builder.GetOrCreateEngineByName(engineHost, $"{dataPort.Name} Engine", "Generated for DataPort");

        _builder.Editors.DataPort.AssignEngine(engine, dataPort);

        LogDataPortAdded(Logger, dataPort.Name, engine.Name);
    }

    private async Task Editor_LoadFunctionBlockDesignsRequested()
        => await LoadFunctionBlockService.LoadPackageReferences(_builder?.Cache.ClusterDependencies.ToArray());

    private async Task ConfirmLeave(LocationChangingContext context)
    {
        if (!_hasUnsavedChanges)
            return;

        var confirmed = await Js.InvokeAsync<bool>("window.confirm", $"{CommonVocabulary.Warning}! {Localization.ClusterEditor.UnsavedChangesMayGetLost}");
        if (!confirmed)
        {
            context.PreventNavigation();
        }
    }

    private void Builder_ClusterChanged(object? sender, EventArgs e)
    {
        var old = _hasUnsavedChanges;
        _hasUnsavedChanges = true;

        if (old != _hasUnsavedChanges)
            InvokeAsync(StateHasChanged);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SaveService_ClusterChanged {Action} received. triggered load latest version")]
    private static partial void LogLoadTriggered(ILogger logger, CrudAction action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned DataPort {DataPortName} to Engine {EngineName}")]
    private static partial void LogDataPortAdded(ILogger logger, string dataPortName, string engineName);
}
