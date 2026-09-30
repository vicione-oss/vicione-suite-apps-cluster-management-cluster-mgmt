using ClusterManagement.Client.IoddManagement.Models;
using ViciOne.Ui.MonochromeIcons.Assets.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.TreeEditor.Builder.Interface;
using ViciOne.Ui.TreeEditor.Builder.Interface.Icons;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal class IoddTreeAdapter(IMonochromeIconSvgMarkupProvider iconMarkupProvider) : TreeAdapter, IDisposable
{
    private readonly Dictionary<(MonochromeIconName Name, MonochromeIconSize Size), IIcon> _icons = [];
    private Task? _initializeTask;

    private readonly Dictionary<int, RepositoryNode> _repositoryNodes = new()
    {
        { 0, new RepositoryNode() { RepositoryId = 0, DisplayText = Localization.Common.IoddRepository, Expanded = true, }},
    };

    public event Action<IEnumerable<DeviceView>>? SelectionChanged;

    public void Clear()
    {
        foreach (var repositoryNode in _repositoryNodes.Values)
        {
            repositoryNode.VendorNodes.Clear();
            Builder.Reset();
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _initializeTask ??= InitializeCore(cancellationToken);

    private async Task InitializeCore(CancellationToken cancellationToken)
        => await LoadIcon(MonochromeIconName.Folder, MonochromeIconSize.Medium, cancellationToken);

    private async Task LoadIcon(MonochromeIconName iconName, MonochromeIconSize iconSize, CancellationToken cancellationToken)
    {
        var key = (iconName, iconSize);

        if (_icons.ContainsKey(key))
            return;

        var svgMarkup = await iconMarkupProvider.GetSvgMarkupAsync(iconName, iconSize, cancellationToken);

        _icons[(iconName, iconSize)] = string.IsNullOrEmpty(svgMarkup)
            ? new SvgIcon(string.Empty) { CssClasses = iconName.GetCssClasses(iconSize) }
            : new SvgIcon(svgMarkup);
    }

    public TreeSelectionKey? SelectedNodeKey
        => Builder.Selection.SelectedNodes.FirstOrDefault() switch
        {
            RepositoryNode n => new TreeSelectionKey(n.RepositoryId, null, null),
            VendorNode n => new TreeSelectionKey(n.RepositoryNode.RepositoryId, n.VendorId, null),
            DeviceFamilyNode n => new TreeSelectionKey(n.VendorNode.RepositoryNode.RepositoryId, n.VendorNode.VendorId, n.DisplayText),
            _ => null
        };

    public bool SetSelection(TreeSelectionKey? value)
    {
        if (value is null)
        {
            foreach (var node in Builder.Selection.SelectedNodes.ToArray())
                Builder.Selection.ChangeSelection(node, false);
            return true;
        }

        if (value.VendorId is not null && value.DeviceFamilyName is not null)
        {
            if (!_repositoryNodes.TryGetValue(value.RepositoryId, out var repositoryNode))
                return false;
            if (!repositoryNode.VendorNodes.TryGetValue(value.VendorId.Value, out var vendorNode))
                return false;
            if (!vendorNode.DeviceFamilyNodes.TryGetValue(value.DeviceFamilyName, out var deviceFamilyNode))
                return false;

            Builder.Expansion.ExpandToNode(deviceFamilyNode);
            Builder.Selection.ChangeSelection(deviceFamilyNode, true);
        }
        else if (value.VendorId is not null)
        {
            if (!_repositoryNodes.TryGetValue(value.RepositoryId, out var repositoryNode))
                return false;
            if (!repositoryNode.VendorNodes.TryGetValue(value.VendorId.Value, out var vendorNode))
                return false;

            Builder.Expansion.ExpandToNode(vendorNode);
            Builder.Selection.ChangeSelection(vendorNode, true);
        }
        else
        {
            if (!_repositoryNodes.TryGetValue(value.RepositoryId, out var repositoryNode))
                return false;

            Builder.Expansion.ExpandToNode(repositoryNode);
            Builder.Selection.ChangeSelection(repositoryNode, true);
        }

        return true;
    }

    public override void Setup()
    {
        Builder.DragAndDrop.EnableInbound = false;
        Builder.DragAndDrop.EnableInternal = false;
        Builder.DragAndDrop.EnableOutbound = false;
    }

    public void AddDevice(DeviceView device, bool expandVendor = true)
    {
        var repositoryNode = GetDefaultRepositoryNode();

        if (!repositoryNode.VendorNodes.TryGetValue(device.VendorId, out var vendorNode))
        {
            vendorNode = new()
            {
                RepositoryNode = repositoryNode,
                VendorId = device.VendorId,
                DisplayText = device.VendorName,
            };
            repositoryNode.VendorNodes.Add(device.VendorId, vendorNode);
            Builder.Notifications.NotifyChildrenChanged(repositoryNode);
        }

        if (expandVendor)
            Builder.Expansion.ChangeExpansion(vendorNode, true);

        if (!vendorNode.DeviceFamilyNodes.TryGetValue(device.DeviceFamily, out var deviceFamilyNode))
        {
            deviceFamilyNode = new()
            {
                VendorNode = vendorNode,
                DisplayText = device.DeviceFamily,
            };
            vendorNode.DeviceFamilyNodes.Add(device.DeviceFamily, deviceFamilyNode);
            Builder.Notifications.NotifyChildrenChanged(vendorNode);
        }

        deviceFamilyNode.Devices.Add(device);
        Builder.Notifications.NotifyChildrenChanged(deviceFamilyNode);
    }

    public void CoupleTreeBuilderEvents()
        // builder is available after the adapter was passed to TreeBuilder.SetAdapter method
        => Builder.Selection.SelectionChanged += OnSelectionChanged;

    public void DeleteDevice(int vendorId, int deviceId)
    {
        var repositoryNode = GetDefaultRepositoryNode();

        if (!repositoryNode.VendorNodes.TryGetValue(vendorId, out var vendorNode))
            return;

        var updatedDeviceFamilyNodes = vendorNode.DeviceFamilyNodes.Where(df => df.Value.Devices.RemoveAll(d => d.DeviceId == deviceId) > 0).ToArray();

        foreach (var (key, node) in updatedDeviceFamilyNodes)
        {
            if (node.Devices.Count == 0)
            {
                vendorNode.DeviceFamilyNodes.Remove(key);

                if (vendorNode.DeviceFamilyNodes.Count == 0)
                {
                    repositoryNode.VendorNodes.Remove(vendorId);
                    Builder.Notifications.NotifyChildrenChanged(repositoryNode);
                }
                else
                {
                    Builder.Notifications.NotifyChildrenChanged(vendorNode);
                }
            }
        }
    }

    /// <summary>
    /// Gets the repository root, so all devices are listed as long as the user hasn't selected a node yet.
    /// </summary>
    public TreeSelectionKey? GetDefaultSelectioNode()
        => _repositoryNodes.Values.FirstOrDefault() is { } repositoryNode
            ? new(repositoryNode.RepositoryId, null, null)
            : null;

    public void Dispose()
        => Builder?.Selection.SelectionChanged -= OnSelectionChanged;

    public void ExpandFirstVendor()
    {
        if (_repositoryNodes[0].VendorNodes.Count > 0)
            Builder.Expansion.ChangeExpansion(_repositoryNodes[0].VendorNodes.Values.OrderBy(n => n.DisplayText).First(), true);
    }

    public override IEnumerable<ITreeNode> GetChildren(ITreeNode node)
        => node switch
        {
            RepositoryNode n => n.VendorNodes.Values.OrderBy(n => n.DisplayText),
            VendorNode n => n.DeviceFamilyNodes.Values.OrderBy(n => n.DisplayText),
            _ => []
        };

    public override IEnumerable<IIcon> GetIcons(ITreeNode node)
    {
        if (node is RepositoryNode or VendorNode or DeviceFamilyNode && _icons.TryGetValue((MonochromeIconName.Folder, MonochromeIconSize.Medium), out var icon))
            return [icon];
        return [];
    }

    public override ITreeNode? GetParent(ITreeNode node)
        => node switch
        {
            VendorNode n => n.RepositoryNode,
            DeviceFamilyNode n => n.VendorNode,
            _ => null
        };

    public bool HasDeviceNodes()
        => HasChildren(GetDefaultRepositoryNode());

    public override IEnumerable<ITreeNode> GetRootNodes()
        => _repositoryNodes.Values;

    private void OnSelectionChanged(ITreeNode node, bool selected)
    {
        List<DeviceView> devices = [];

        foreach (var selectedNode in Builder.Selection.SelectedNodes)
        {
            if (selectedNode is RepositoryNode repositoryNode)
                devices.AddRange(repositoryNode.VendorNodes.SelectMany(n => n.Value.DeviceFamilyNodes.Values.SelectMany(n => n.Devices)));
            if (selectedNode is VendorNode vendorNode)
                devices.AddRange(vendorNode.DeviceFamilyNodes.Values.SelectMany(n => n.Devices));
            if (selectedNode is DeviceFamilyNode deviceFamilyNode)
                devices.AddRange(deviceFamilyNode.Devices);
        }

        SelectionChanged?.Invoke(devices);
    }

    public override bool HasChildren(ITreeNode node)
        => node switch
        {
            RepositoryNode n => n.VendorNodes.Count > 0,
            VendorNode n => n.DeviceFamilyNodes.Count > 0,
            _ => false
        };

    private RepositoryNode GetDefaultRepositoryNode()
        => _repositoryNodes[0];

    public override string GetDisplayText(ITreeNode treeNode)
    {
        if (treeNode is IIoddTreeNode n)
            return n.DisplayText;

        return $"{treeNode.Id}";
    }
}
