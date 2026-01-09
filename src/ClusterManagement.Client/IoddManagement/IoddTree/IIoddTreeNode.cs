using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal interface IIoddTreeNode : ITreeNode
{
    string DisplayText { get; }
}
