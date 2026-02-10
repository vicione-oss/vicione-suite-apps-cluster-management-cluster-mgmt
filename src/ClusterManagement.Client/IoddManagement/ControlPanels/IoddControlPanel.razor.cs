using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.IoddManagement.Extensions;
using ClusterManagement.Client.IoddManagement.IoddTree;
using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Client.Localization;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.Shared.Dx.Components;
using ViciOne.Ui.TreeEditor.Builder;

namespace ClusterManagement.Client.IoddManagement.ControlPanels;

[ControlPanelCategory<IoddControlPanelCategoryDescriptor>]
public sealed partial class IoddControlPanel : IEventConsumer<IoddChanged>, IEventConsumer<IoddError>, IEventConsumer<AutoIoddDownloadChanged>
{

    private string _filterText = string.Empty;
    private IQueryable<DeviceView> _itemsQueryable = Enumerable.Empty<DeviceView>().AsQueryable();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private readonly TreeBuilder _treeBuilder = new();
    private DxDialog? _refDeleteIoddsDialog;
    private bool _waitingForAutoDownloadChanged;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private IJsInterop JavaScriptInterop { get; set; } = default!;
    [Inject] private IIoddService IoddService { get; set; } = default!;
    [Inject] private IoddTreeAdapter TreeAdapter { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;

    private GridItemSelection<IoddId> ItemSelection { get; } = [];

    private async Task AutoDownloadChanged(bool value)
    {
        _waitingForAutoDownloadChanged = true;
        await IoddService.SetIsAutoDownloading(value);
    }

    private IQueryable<DeviceView> FilterItems(IQueryable<DeviceView> devices)
        => devices
            .Where(i => i.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                     || i.DeviceId.ToString(CultureInfo.InvariantCulture).Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    public async Task Consume(ClientContext<IoddChanged> context, CancellationToken cancellationToken)
    {
        var deviceId = context.Message.Iodd.DeviceId;
        var vendorId = context.Message.Iodd.VendorId;

        if (context.Message.Action == CrudAction.Deleted)
        {
            TreeAdapter.DeleteDevice(vendorId, deviceId);

            if (!TreeAdapter.SetSelection(State.SelectedTreeNodeKey))
            {
                State.SelectedTreeNodeKey = TreeAdapter.GetDefaultSelectioNode();
                TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
            }
        }
        // Todo: event is currently only thrown if the user uploads an iodd file, it should be sent when one is automatically uploaded too
        else if (context.Message.Action == CrudAction.Created)
        {
            if (context.Message.DeviceInfo is null)
                throw new InvalidOperationException("The device cannot be null.");

            foreach (var deviceVariant in context.Message.DeviceInfo.DeviceDescription.DeviceIdentity.Variants)
            {
                var deviceView = context.Message.DeviceInfo.Create(deviceVariant);
                TreeAdapter.AddDevice(deviceView);
            }
            TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
        }

        await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<IoddError> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error.ErrorCode == IoddError.ExtractZipError)
            MessageBannerService.ShowMessageBanner(MessageType.Error, Common.UploadFailed);
        else
            MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    public async Task Consume(ClientContext<AutoIoddDownloadChanged> context, CancellationToken cancellationToken)
    {
        State.AutoDownload = context.Message.Value;
        _waitingForAutoDownloadChanged = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnDeleteIoddsCloseAsync()
        => await _refDeleteIoddsDialog!.CloseAsync();

    private async Task OnDeleteIoddsOkAsync()
    {
        var ids = ItemSelection.Distinct();

        foreach (var id in ids)
        {
            await IoddService.DeleteIodd(id.VendorId, id.DeviceId);
        }

        await _refDeleteIoddsDialog!.CloseAsync();
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

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await TreeAdapter.InitializeTreeIconsFromHttp();

        TreeAdapter.SelectionChanged += OnIoddTreeAdapterSelectionChanged;

        _treeBuilder.SetAdapter(TreeAdapter);
        TreeAdapter.CoupleTreeBuilderEvents();

        ItemSelection.Changed += OnItemSelectionChanged;

        await AddInitialDevices();

        State.AutoDownload = await IoddService.GetIsAutoDownloading();

        _subscriptionHandles.Add(Mediator.Register<IoddChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<IoddError>(this));
        _subscriptionHandles.Add(Mediator.Register<AutoIoddDownloadChanged>(this));

        State.SelectedTreeNodeKey ??= TreeAdapter.GetDefaultSelectioNode();

        if (State.SelectedTreeNodeKey is not null)
            TreeAdapter.SetSelection(State.SelectedTreeNodeKey);
    }

    private async Task AddInitialDevices()
    {
        if (TreeAdapter is null)
            throw new InvalidOperationException("Device tree must be initialized");

        var devices = await IoddService.GetIODevices();

        foreach (var device in devices)
        {
            foreach (var deviceVariant in device.DeviceDescription.DeviceIdentity.Variants)
            {
                var deviceView = device.Create(deviceVariant);
                TreeAdapter.AddDevice(deviceView, false);
            }
        }

        TreeAdapter.ExpandFirstVendor();
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
        var image = await IoddService.GetIODeviceImage(item.VendorId, item.DeviceId, item.DeviceImage);
        var result = await ControlPanelRequest.Send<IoddDetailsControlPanel, IoddDetailsControlPanelState>(s =>
        {
            s.DeviceVariant = item;
            s.DeviceImage = image;
        });
    }

    private async Task OnIoddDeleteClickAsync()
        => await _refDeleteIoddsDialog!.OpenAsync();

    private async Task OnIoddDownloadClickAsync()
    {
        var ids = ItemSelection.Distinct();

        foreach (var id in ids)
        {
            var (file, name) = await IoddService.DownloadIodd(id.VendorId, id.DeviceId);
            using var stream = new MemoryStream();
            await stream.WriteAsync(file);
            stream.Position = 0;
            await stream.FlushAsync();
            await JavaScriptInterop.DownloadAs(stream, $"{Path.GetFileNameWithoutExtension(name)}.zip");
        }
    }

    private async Task OnIoddUploadClickAsync()
        => await JavaScript.InvokeVoidAsync("triggerUploadClick");

    private string GetFooterString()
    {
        var elements = CommonVocabulary.Elements;

        if (_itemsQueryable.Count() is 1)
            elements = CommonVocabulary.Element;

        if (ItemSelection.Count <= 0)
            return $"{_itemsQueryable.Count()} {elements}";

        return $"{_itemsQueryable.Count()} {elements} | {_itemsQueryable.Where(d => ItemSelection.Select(s => s.DeviceId).Contains(d.DeviceId)).Count()} {CommonVocabulary.Selected}";
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

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
}
