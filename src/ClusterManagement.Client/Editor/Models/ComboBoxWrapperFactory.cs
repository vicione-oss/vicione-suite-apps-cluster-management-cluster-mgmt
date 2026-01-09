namespace ClusterManagement.Client.Editor.Models;

internal static class ComboBoxWrapperFactory
{
    public static List<ComboBoxWrapper<T?>> ListWithNullItem<T>() where T : struct, Enum => ListWithNullItem(Enum.GetValues<T>()
        .Select(k => new ComboBoxWrapper<T?>
        {
            DisplayText = k.ToString(),
            Value = k
        }));

    public static List<ComboBoxWrapper<T>> ListWithNullItem<T>(IEnumerable<ComboBoxWrapper<T>>? items)
    {
        var lst = new List<ComboBoxWrapper<T>>
        {
            new () { DisplayText = "None", Value = default }
        };

        if (items is not null)
            lst.AddRange(items);

        return lst;
    }
}
