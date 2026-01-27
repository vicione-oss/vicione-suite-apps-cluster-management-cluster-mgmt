using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Editor.ControlPanels.General;

public sealed class GeneralControlPanelDescriptor : IControlPanelDescriptor<GeneralControlPanel>
{
    public string Title => CommonVocabulary.General;
    public string IconPath => ModuleAssetHelper.GetModuleIconPath<ClusterManagementClientModule>("cluster-editor.svg");
}
