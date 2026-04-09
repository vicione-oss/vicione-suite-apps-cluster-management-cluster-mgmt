using System.Globalization;
using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Client.Editor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Editor.Dialogs;

public sealed partial class LoadFunctionBlockDialog : IDisposable
{
    private readonly FunctionBlockTreeRoot _functionBlockTreeRoot = new();
    private readonly string _localizeCollapseAllGroups = string.Format(CultureInfo.CurrentCulture, UserActions.CollapseSomething, CommonVocabulary.All);
    private readonly string _localizeExpandAllGroups = string.Format(CultureInfo.CurrentCulture, UserActions.ExpandSomething, CommonVocabulary.All);

    private bool _hasCheckedNodes;
    private bool _initialized;
    private Dialog? _refDialog;
    private string _filter = string.Empty;
    private readonly Dictionary<string, CheckedClusterDependencyModel> _checkedFunctionBlocks = [];

    [Inject]
    private LoadFunctionBlocksService LoadService { get; set; } = default!;

    [Inject]
    private ILogger<LoadFunctionBlockDialog> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        LoadService.PropertyChanged += OnPropertyChanged;
        FunctionBlockModel.FunctionBlockCheckChanged += OnFunctionBlockModelCheckChanged;
    }

    private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(LoadService.DisplayDialog))
            await InvokeAsync(StateHasChanged);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!_hasCheckedNodes && LoadService.Dependencies is not null && !_initialized)
        {
            SetTreeList(LoadService.Dependencies);
            _initialized = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(LoadService.DisplayDialog);

    private void SetTreeList(IEnumerable<ClusterDependency> dependencies)
    {
        _functionBlockTreeRoot.FunctionBlocks.Clear();
        var checkedFbs = FastCloner.FastCloner.DeepClone(_checkedFunctionBlocks)
            ?? throw new InvalidOperationException("Cannot clone checked FunctionBlocks.");

        foreach (var dependency in dependencies)
        {
            var splitName = dependency.Name.Split(".");

            SetTreeElement(splitName, 0, splitName.First(), dependency, checkedFbs);
        }

        if (_functionBlockTreeRoot.FunctionBlocks.Count > 0)
            _functionBlockTreeRoot.FunctionBlocks.First().RefreshParentCheckStates();
    }

    private void SetTreeElement(string[] splitName, int index, string nameElement, ClusterDependency dependency, Dictionary<string, CheckedClusterDependencyModel> checkedFbs, FunctionBlockModel? parent = null)
    {
        var element = parent is not null
            ? parent.FunctionBlocks.FirstOrDefault(e => e.Name == nameElement)
            : _functionBlockTreeRoot.FunctionBlocks.FirstOrDefault(e => e.Name == nameElement);

        if (element is not null && index < splitName.Length - 1)
        {
            if (checkedFbs.TryGetValue(nameElement, out _))
            {
                element.Checked = checkedFbs[nameElement].Checked;
            }

            SetTreeElement(splitName, index + 1, splitName[index + 1], dependency, checkedFbs, element);
            return;
        }

        if (element is null && index < splitName.Length)
        {
            element = new FunctionBlockModel() { Name = nameElement, Parent = parent };

            if (parent is not null)
            {
                parent.FunctionBlocks.Add(element);
                parent.FunctionBlocks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            }
            else
            {
                _functionBlockTreeRoot.FunctionBlocks.Add(element);
                _functionBlockTreeRoot.FunctionBlocks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            }

            if (checkedFbs.TryGetValue(nameElement, out _))
                element.Checked = checkedFbs[nameElement].Checked;

            if (index + 1 < splitName.Length)
                SetTreeElement(splitName, index + 1, splitName[index + 1], dependency, checkedFbs, element);
            else
                element.Dependency = dependency;
        }
    }

    private void CloseDialog()
        => LoadService.DisplayDialog = false;

    private void OnCollapseAllClick()
        => _functionBlockTreeRoot.ToggleExpandAll(false);

    private void OnExpandAllClick()
        => _functionBlockTreeRoot.ToggleExpandAll(true);

    private async Task OnOkButtonClick()
    {
        try
        {
            if (_checkedFunctionBlocks.Count < 1)
                return;

            List<ClusterDependency> currentlyCheckedDependencies = [];
            List<ClusterDependency> loadDependencies = [];

            foreach (var fb in _checkedFunctionBlocks.Where(k => k.Value.ClusterDependency is not null))
                currentlyCheckedDependencies.Add(fb.Value.ClusterDependency!);

            if (LoadService.SelectedDependencies is not null)
                loadDependencies = [.. LoadService.SelectedDependencies];

            foreach (var dependency in currentlyCheckedDependencies)
                loadDependencies.Add(dependency);

            LoadService.SelectedDependencies = loadDependencies;

            await LoadService.LoadSelectedFunctionBlocks();

            if (LoadService.Dependencies is not null && LoadService.SelectedDependencies is not null)
                SetTreeList(LoadService.Dependencies.Except(LoadService.SelectedDependencies));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error occured while loading the FunctionBlock designs.");
        }
        finally
        {
            LoadService.DisplayDialog = false;
        }
    }

    private async Task ShowFilteredFunctionBlocks()
    {
        if (LoadService.Dependencies is not null && LoadService.SelectedDependencies is not null)
            SetTreeList(LoadService.Dependencies.Except(LoadService.SelectedDependencies).Where(d => d.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)));

        await InvokeAsync(StateHasChanged);
    }

    private void ResetTree()
    {
        _checkedFunctionBlocks.Clear();

        foreach (var fb in _functionBlockTreeRoot.FunctionBlocks)
        {
            fb.Checked = false;
            fb.SetCheckValueToChildren(false);
        }

        _filter = string.Empty;

        if (LoadService.Dependencies is not null && LoadService.SelectedDependencies is not null)
            SetTreeList(LoadService.Dependencies.Except(LoadService.SelectedDependencies));
    }

    private void OnCheckedChanged(bool? checkValue)
    {
        checkValue ??= true;

        foreach (var fb in _functionBlockTreeRoot.FunctionBlocks)
        {
            fb.Checked = checkValue;
            fb.SetCheckValueToChildren(checkValue);
        }

        _hasCheckedNodes = checkValue ?? true;
    }

    private void OnFunctionBlockModelCheckChanged(FunctionBlockModel sender)
    {
        if (_checkedFunctionBlocks.TryGetValue(sender.Name, out _))
        {
            if (sender.Checked is true or null)
                _checkedFunctionBlocks[sender.Name].Checked = sender.Checked;
            else
                _checkedFunctionBlocks.Remove(sender.Name);
        }
        else
        {
            if (sender.Checked is true or null)
                _checkedFunctionBlocks.Add(sender.Name, new CheckedClusterDependencyModel(sender.Checked, sender.Dependency));
        }
    }

    public void Dispose()
    {
        LoadService.PropertyChanged -= OnPropertyChanged;
        FunctionBlockModel.FunctionBlockCheckChanged -= OnFunctionBlockModelCheckChanged;
    }
}
