using System.Globalization;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Client.Localization;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.TreeEditor.Builder;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

[ControlPanelCategory<IoddControlPanelCategoryDescriptor>]
public sealed partial class IoddControlPanel
{
    private readonly string _uploadIconCssClasses = @MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _importIconCssClasses = @MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    private readonly string _deleteIconCssClasses = @MonochromeIconName.Delete.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private SettingsFieldFileUpload<IoddUpload>? _fileUpload;
    // The upload field cancels a running upload when it gets a filename or ticket other than its own, so both are passed back as it reports them.
    private string? _uploadFilename;
    private IUploadTicket? _uploadTicket;
    private IQueryable<DeviceView> _itemsQueryable = Enumerable.Empty<DeviceView>().AsQueryable();
    private readonly TreeBuilder _treeBuilder = new();
    private Dialog? _refDeleteIoddsDialog;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private IJsInterop JavaScriptInterop { get; set; } = default!;
    [Inject] private IIoddService IoddService { get; set; } = default!;
    [Inject] private IoddTreeAdapter TreeAdapter { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private ILogger<IoddControlPanel> Logger { get; set; } = default!;
    [Inject] private IUploadTicketFactory UploadTicketFactory { get; set; } = default!;
    [Inject] private IStreamUploadHandler<IoddUpload> StreamUploadHandler { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    private GridItemSelection<IoddSelectionId> ItemSelection { get; } = [];

    protected override async ValueTask DisposeAsyncCore()
    {
        IoddService.AutoIoddDownloadChanged -= IoddService_AutoIoddDownloadChanged;
        IoddService.IoddChanged -= IoddService_IoddChanged;
        State.Changed -= State_Changed;

        _treeBuilder.Dispose();

        if (TreeAdapter is not null)
        {
            TreeAdapter.SelectionChanged -= OnIoddTreeAdapterSelectionChanged;
            TreeAdapter.Dispose();
        }

        if (ItemSelection is not null)
        {
            ItemSelection.Changed -= OnItemSelectionChanged;
            ItemSelection.Clear();
        }

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await TreeAdapter.InitializeAsync();

        IoddService.AutoIoddDownloadChanged += IoddService_AutoIoddDownloadChanged;
        IoddService.IoddChanged += IoddService_IoddChanged;

        TreeAdapter.SelectionChanged += OnIoddTreeAdapterSelectionChanged;
        State.Changed += State_Changed;

        _treeBuilder.SetAdapter(TreeAdapter);
        TreeAdapter.CoupleTreeBuilderEvents();

        ItemSelection.Changed += OnItemSelectionChanged;

        // Ensure to fill the tree if the async state reset was not yet completed
        if (!TreeAdapter.HasDeviceNodes())
            InitTreeAdapterDevices();
    }

    private async void State_Changed(ControlPanelStateChangedEventArgs args)
    {
        var stateHasChanged = false;

        if (args.PropertyNames.Contains(nameof(State.Devices)))
        {
            // depending an async handling we are already connected to the state changed event before the async reset completed,
            // so we need to ensure that the tree is filled with the devices from the state
            ItemSelection.Clear();
            InitTreeAdapterDevices();
            stateHasChanged = true;
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private void InitTreeAdapterDevices()
    {
        if (TreeAdapter is null)
            return;

        TreeAdapter.Clear();

        foreach (var device in State.Devices)
        {
            TreeAdapter.AddDevice(device, false);
        }

        TreeAdapter.ExpandFirstVendor();

        State.SelectedTreeNodeKey ??= TreeAdapter.GetDefaultSelectioNode();
        if (State.SelectedTreeNodeKey is not null)
            TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
    }

    private async Task IoddService_IoddChanged(IoddChanged change, CancellationToken cancellationToken)
    {
        // Errors never get here: IoddService completes the waiting call with them instead of raising the event.
        var deviceId = change.Iodd.DeviceId;
        var vendorId = change.Iodd.VendorId;

        if (change.Action == CrudAction.Deleted)
        {
            TreeAdapter.DeleteDevice(vendorId, deviceId);

            if (!TreeAdapter.SetSelection(State.SelectedTreeNodeKey))
            {
                State.SelectedTreeNodeKey = TreeAdapter.GetDefaultSelectioNode();
                TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
            }
        }
        // Todo: event is currently only thrown if the user uploads an iodd file, it should be sent when one is automatically uploaded too
        else if (change.Action == CrudAction.Created)
        {
            if (change.DeviceInfo is null)
                return;

            var variants = change.DeviceInfo.CreateDeviceVariants();
            foreach (var deviceVariant in variants)
            {
                State.Devices.Add(deviceVariant);
                TreeAdapter.AddDevice(deviceVariant);
            }

            TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task IoddService_AutoIoddDownloadChanged(AutoIoddDownloadChanged change, CancellationToken cancellationToken)
    {
        State.WaitingForAutoDownloadChange = true;
        State.AutoDownload = change.Value;
        await InvokeAsync(StateHasChanged);
    }

    private async Task AutoDownloadValueChanged(bool value)
    {
        State.WaitingForAutoDownloadChange = false;
        await IoddService.SetIsAutoDownloading(value);
    }

    private IQueryable<DeviceView> FilterItems(IQueryable<DeviceView> devices)
    {
        if (string.IsNullOrEmpty(State.FilterText))
            return devices.OrderBy(d => d.Name);

        return devices
                .Where(i => i.Name.Contains(State.FilterText, StringComparison.OrdinalIgnoreCase)
                         || i.DeviceId.ToString(CultureInfo.InvariantCulture).Contains(State.FilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private void OnItemSelectionChanged(ViciOne.Ui.Blazor.Components.Grid.Services.GridItemSelectionChangedEventArgs<IoddSelectionId> obj)
    {
        // Each Iodd can have multiple variants, which are distinguished by their ProductId.
        // When an Iodd is selected or deleted, all variants with the same VendorId and DeviceId must also be selected or deleted.
        // To prevent recursion during the add and delete operations, the event is temporarily unsubscribed.
        ItemSelection.Changed -= OnItemSelectionChanged;
        try
        {
            foreach (var added in obj.ItemsAdded.ToList())
            {
                var devices = _itemsQueryable
                    .Where(d => d.VendorId == added.VendorId && d.DeviceId == added.DeviceId);

                ItemSelection.AddRange(devices.Select(d => new IoddSelectionId(d.VendorId, d.DeviceId, d.ProductId)));
            }

            foreach (var removed in obj.ItemsRemoved.ToList())
            {
                ItemSelection.Remove(id => id.VendorId == removed.VendorId && id.DeviceId == removed.DeviceId);
            }
        }
        finally
        {
            ItemSelection.Changed += OnItemSelectionChanged;
        }

        InvokeAsync(StateHasChanged);
    }

    private void OnIoddTreeAdapterSelectionChanged(IEnumerable<DeviceView> selection)
    {
        State.SelectedTreeNodeKey = TreeAdapter.SelectedNodeKey;
        _itemsQueryable = selection.AsQueryable();

        ItemSelection.Clear();
        InvokeAsync(StateHasChanged);
    }

    private async Task ShowDetailsAsync(DeviceView item)
    {
        try
        {
            var image = await IoddService.GetIODeviceImage(item.VendorId, item.DeviceId, item.DeviceImage);
            var result = await ControlPanelRequest.Send<IoddDetailsControlPanel, IoddDetailsControlPanelState>(s =>
            {
                s.DeviceVariant = item;
                s.DeviceImage = image;
            });
        }
        catch (Exception ex)
        {
            LogError(Logger, ex, "showing details", item.VendorId, item.DeviceId);
        }
    }

    private async Task OnDeleteIoddsCloseAsync()
        => await _refDeleteIoddsDialog!.CloseAsync();

    private async Task OnDeleteIoddsOkAsync()
    {
        var ids = ItemSelection.Distinct().Select(d => new IoddId(d.VendorId, d.DeviceId));

        State.MarkIoddAsDeleted(ids);

        // Delete devices from tree if they are marked for deletion
        foreach (var device in State.DevicesMarkedForDeletion)
        {
            TreeAdapter.DeleteDevice(device.VendorId, device.DeviceId);
            ItemSelection.Where(d => d.VendorId == device.VendorId && d.DeviceId == device.DeviceId).ToList().ForEach(d => ItemSelection.Remove(d));
        }

        if (State.HasChanges())
        {
            _itemsQueryable = _itemsQueryable
                .Where(k => !State.DevicesMarkedForDeletion.Any(device => device.VendorId == k.VendorId
                && device.DeviceId == k.DeviceId));

            await BeginEdit();
        }

        await _refDeleteIoddsDialog!.CloseAsync();
    }

    private async Task OnIoddDeleteClickAsync()
        => await _refDeleteIoddsDialog!.ShowAsync();

    private async Task OnIoddDownloadClickAsync()
    {
        // Each Iodd can have multiple variants sharing the same VendorId/DeviceId, and multiple devices can belong to
        // the same vendor. To avoid redundant downloads, one archive per vendor is requested.
        var vendorGroups = ItemSelection
            .GroupBy(id => id.VendorId)
            .Select(g => (VendorId: g.Key, DeviceIds: (IReadOnlyList<int>)[.. g.Select(id => id.DeviceId).Distinct()]));

        foreach (var (vendorId, deviceIds) in vendorGroups)
        {
            try
            {
                var (file, name) = await IoddService.DownloadIodds(vendorId, deviceIds);
                using var stream = new MemoryStream();
                await stream.WriteAsync(file);
                stream.Position = 0;
                await stream.FlushAsync();
                await JavaScriptInterop.DownloadAs(stream, name);
            }
            catch (Exception ex)
            {
                LogVendorError(Logger, ex, "downloading iodds", vendorId);
                MessageBannerService.ShowMessageBanner(MessageType.Error, Common.DownloadFailed);
            }
        }
    }

    private Task OnIoddUploadStart(IUploadTicket uploadTicket)
    {
        _uploadTicket = uploadTicket;
        return Task.CompletedTask;
    }

    private async Task OnIoddUploadSuccess(StreamUploadSuccessResult successResult)
    {
        // The archive is on the server now, so the field is free for the next file while the backend adds the IODD.
        await ResetUploadAsync();

        try
        {
            var result = await IoddService.AddIodd(successResult.DestinationFile);

            if (result is IoddServiceErrorResult error)
                MessageBannerService.ShowMessageBanner(MessageType.Error, GetUploadErrorMessage(error));
        }
        catch (Exception ex)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, ex.Message);
        }
    }

    /// <summary>
    /// Maps the backend error codes to localized messages; the technical details are logged by the backend.
    /// </summary>
    private static string GetUploadErrorMessage(IoddServiceErrorResult error)
        => error.ErrorCode switch
        {
            IoddChanged.IoddAlreadyExists when error.ExistingDevice is not null
                => string.Format(CultureInfo.CurrentCulture, Localization.IoddControlPanel.IoddAlreadyExists, error.ExistingDevice.GetDisplayName()),
            IoddChanged.ExtractZipError
                or IoddChanged.IoddAlreadyExists
                or IoddChanged.IoddFileNotFound
                or IoddChanged.IoddFileOutsideUploadDirectory => Common.UploadFailed,
            _ => error.ErrorMessage,
        };

    private async Task OnIoddUploadError(StreamUploadErrorResult errorResult)
    {
        await ResetUploadAsync();
        MessageBannerService.ShowMessageBanner(MessageType.Error, errorResult.Message);
    }

    private Task OnIoddUploadCancel()
    {
        // The field has already reset itself.
        _uploadTicket = null;
        return Task.CompletedTask;
    }

    private async Task ResetUploadAsync()
    {
        _uploadTicket = null;

        if (_fileUpload is not null)
            await _fileUpload.ResetAsync();
    }

    private string GetFooterString()
    {
        var elements = CommonVocabulary.ElementPlural;

        if (_itemsQueryable.Count() is 1)
            elements = CommonVocabulary.Element;

        if (ItemSelection.Count <= 0)
            return $"{_itemsQueryable.Count()} {elements}";

        return $"{_itemsQueryable.Count()} {elements} | {_itemsQueryable.Where(d => ItemSelection.Select(s => s.DeviceId).Contains(d.DeviceId)).Count()} {CommonVocabulary.Selected}";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error {Action} for device {VendorId}/{DeviceId}")]
    private static partial void LogError(ILogger<IoddControlPanel> logger, Exception exception, string action, int vendorId, int deviceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error {Action} for vendor {VendorId}")]
    private static partial void LogVendorError(ILogger<IoddControlPanel> logger, Exception exception, string action, int vendorId);
}
