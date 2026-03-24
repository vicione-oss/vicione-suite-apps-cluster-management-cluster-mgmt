using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Editor.Wizards.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Extensions;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
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

    [CascadingParameter]
    public required Task<AuthenticationState> AuthState { get; set; }

    protected override List<Resource> Resources => new List<Resource>()
        .AddClusterEditorResources();

    protected override string PageTitle => Localization.ClusterEditor.ModuleTitle;

    protected override async ValueTask DisposeInternal()
    {
        _builder?.Dispose();
        _subscriptionHandles.Dispose();

        await _tokenSource.CancelAsync();
        _tokenSource.Dispose();

        LoadService.ClusterLoaded -= OnClusterLoaded;
        SaveService.ClusterChanged -= OnClusterChanged;

        ClusterEditorManagement.ExportRequested -= OnExportRequested;
        ClusterEditorManagement.ImportRequested -= OnImportRequested;
        ClusterEditorManagement.SaveRequested -= SaveService.SaveRequested;
        ClusterEditorManagement.LoadFunctionBlockDesignsRequested -= OnLoadFunctionBlockDesignsRequested;

        LayoutService.IsLoadingOverlayVisible = false;

        await base.DisposeInternal();
    }

    protected override async Task OnInitializedAsync()
    {
        var username = (await AuthState).User.Identity?.Name ?? "Anonymous";
        var request = new GetClusterEditorSettingsRequest(username);
        var response = await Mediator.Request<GetClusterEditorSettingsRequest, GetClusterEditorSettingsResponse>(request, _tokenSource.Token);

        var settings = response.RequestError is not null ? new EditorSettings() { UserId = username } : response.Settings!;

        SettingsService.SetGridMode(settings.GetGridMode());
        SettingsService.SetMinimapNodeColoring(settings.MinimapNodeColoring);
        SettingsService.SetNodeAlignmentBorder(settings.NodeAlignmentBorder);
        SettingsService.SetPanBehavior(settings.PanBehavior);
        SettingsService.SetSimplifiedView(settings.SimplifiedView);

        LayoutService.IsLoadingOverlayVisible = true;
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterResourcesLoadedAsync()
    {
        // editor needs to be rendered after the js scripts were loaded!
        _resourcesLoaded = true;
        await InvokeAsync(StateHasChanged);

        _subscriptionHandles.Add(Mediator.Register<ClusterChanged>(SaveService));
        _subscriptionHandles.Add(Mediator.Register<CommitClusterChangeFailed>(SaveService));

        // connect editor events to our services
        LoadService.ClusterLoaded += OnClusterLoaded;

        // will only be triggered for save executed by ourselves
        SaveService.ClusterChanged += OnClusterChanged;

        ClusterEditorManagement.ExportRequested += OnExportRequested;
        ClusterEditorManagement.ImportRequested += OnImportRequested;
        ClusterEditorManagement.SaveRequested += SaveService.SaveRequested;
        ClusterEditorManagement.LoadFunctionBlockDesignsRequested += OnLoadFunctionBlockDesignsRequested;

        await LoadService.TryLoadLatestVersion();

        LayoutService.IsLoadingOverlayVisible = false;
    }

    private async Task OnExportRequested()
        => await ExportService.DownloadClusterJson(_builder?.Cluster);

    private async Task OnImportRequested()
        => await WizardDialogService.OpenDialog<ImportWizardService>(_builder);

    private async void OnClusterChanged(object? sender, ClusterChanged change)
    {
        Logger.LogInformation("{Name} {Action} received. triggered load latest version", nameof(OnClusterChanged), change.Action);

        await LoadService.TryLoadLatestVersion();
    }

    private void OnClusterLoaded(object? _, ClusterBuilder builder)
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

        Logger.LogInformation("Assigned DataPort {DataPortName} to Engine {EngineName}", dataPort.Name, engine.Name);
    }

    private async Task OnLoadFunctionBlockDesignsRequested()
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
}
