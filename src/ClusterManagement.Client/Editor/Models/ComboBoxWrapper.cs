namespace ClusterManagement.Client.Editor.Models;

internal sealed class ComboBoxWrapper<T>
{
    public string DisplayText { get; set; } = string.Empty;

    public T? Value { get; set; }
}
