using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Editor.ControlPanels.General;

public sealed class GeneralControlPanelDescriptor : IControlPanelDescriptor<GeneralControlPanel>
{
    public string Title => CommonVocabulary.General;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<ClusterManagementClientModule>("cluster-editor.svg");
}
