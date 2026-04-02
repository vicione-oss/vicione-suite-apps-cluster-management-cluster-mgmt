using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.IoddManagement.ControlPanels.Services;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Client.Localization;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.Shared.Dx.Components;
using ViciOne.Ui.TreeEditor.Builder;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

[ControlPanelCategory<IoddControlPanelCategoryDescriptor>]
public sealed partial class IoddControlPanel
{
    private readonly string _uploadIconCssClasses = @MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _importIconCssClasses = @MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _deleteIconCssClasses = @MonochromeIconName.Delete.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private IQueryable<DeviceView> _itemsQueryable = Enumerable.Empty<DeviceView>().AsQueryable();
    private readonly TreeBuilder _treeBuilder = new();
    private DxDialog? _refDeleteIoddsDialog;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private IJsInterop JavaScriptInterop { get; set; } = default!;
    [Inject] private IIoddService IoddService { get; set; } = default!;
    [Inject] private IoddTreeAdapter TreeAdapter { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private ILogger<IoddControlPanel> Logger { get; set; } = default!;

    private GridItemSelection<IoddId> ItemSelection { get; } = [];

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
        await TreeAdapter.InitializeTreeIconsFromHttp();

        IoddService.AutoIoddDownloadChanged += IoddService_AutoIoddDownloadChanged;
        IoddService.IoddChanged += IoddService_IoddChanged;

        TreeAdapter.SelectionChanged += OnIoddTreeAdapterSelectionChanged;
        State.Changed += State_Changed;

        _treeBuilder.SetAdapter(TreeAdapter);
        TreeAdapter.CoupleTreeBuilderEvents();

        // Ensure to fill the tree if the async state reset was not yet completed
        if (!TreeAdapter.HasDeviceNodes())
            InitTreeAdapterDevices();

        ItemSelection.Changed += OnItemSelectionChanged;
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
        if (change.Error is not null)
        {
            if (change.Error.ErrorCode == IoddChanged.ExtractZipError)
                MessageBannerService.ShowMessageBanner(MessageType.Error, Common.UploadFailed);
            else
                MessageBannerService.ShowMessageBanner(MessageType.Error, change.Error.Message);

            return;
        }

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

    private void OnItemSelectionChanged(ViciOne.Ui.Blazor.Components.Grid.Services.GridItemSelectionChangedEventArgs<IoddId> obj)
        => InvokeAsync(StateHasChanged);

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
            Logger.LogError(ex, "Error showing details for device {VendorId}/{DeviceId}", item.VendorId, item.DeviceId);
        }
    }

    private async Task OnDeleteIoddsCloseAsync()
        => await _refDeleteIoddsDialog!.CloseAsync();

    private async Task OnDeleteIoddsOkAsync()
    {
        var ids = ItemSelection.Distinct();

        State.MarkIoddAsDeleted(ids);

        // Delete devices from tree if they are marked for deletion
        foreach (var device in State.DevicesMarkedForDeletion)
        {
            TreeAdapter.DeleteDevice(device.VendorId, device.DeviceId);
            ItemSelection.Remove(device);
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
        => await _refDeleteIoddsDialog!.OpenAsync();

    private async Task OnIoddDownloadClickAsync()
    {
        var ids = ItemSelection.Distinct();

        foreach (var id in ids)
        {
            try
            {
                var (file, name) = await IoddService.DownloadIodd(id.VendorId, id.DeviceId);
                using var stream = new MemoryStream();
                await stream.WriteAsync(file);
                stream.Position = 0;
                await stream.FlushAsync();
                await JavaScriptInterop.DownloadAs(stream, $"{Path.GetFileNameWithoutExtension(name)}.zip");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error downloading iodd for device {VendorId}/{DeviceId}", id.VendorId, id.DeviceId);
                break;
            }
        }
    }

    private async Task OnFileChange(InputFileChangeEventArgs args)
    {
        if (!MessageBannerService.IsValidZipArchive(args))
            return;

        try
        {
            byte[] data;

            await using (var ms = new MemoryStream())
            {
                await args.File.OpenReadStream(ControlPanelConstants.TwoMB).CopyToAsync(ms);
                data = ms.ToArray();
            }

            await IoddService.UploadIodd(data);
        }
        catch (Exception ex)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, ex.Message);
        }
    }

    private async Task OnIoddUploadClickAsync()
        => await JavaScript.InvokeVoidAsync("triggerUploadClick");

    private string GetFooterString()
    {
        var elements = CommonVocabulary.ElementPlural;

        if (_itemsQueryable.Count() is 1)
            elements = CommonVocabulary.Element;

        if (ItemSelection.Count <= 0)
            return $"{_itemsQueryable.Count()} {elements}";

        return $"{_itemsQueryable.Count()} {elements} | {_itemsQueryable.Where(d => ItemSelection.Select(s => s.DeviceId).Contains(d.DeviceId)).Count()} {CommonVocabulary.Selected}";
    }
}
