namespace ClusterManagement.Client.DataflowEvents.Models;

internal static class ValueTypeNames
{
    /// <summary>
    /// Turns an assembly-qualified type name into its readable short form, such as <c>List&lt;Int32&gt;</c>,
    /// without loading the type.
    /// </summary>
    public static string Short(string assemblyQualifiedName)
    {
        var typeName = FullName(assemblyQualifiedName);

        if (typeName.EndsWith("[]", StringComparison.Ordinal))
            return $"{Short(typeName[..^2])}[]";

        var backtick = typeName.IndexOf('`', StringComparison.Ordinal);
        if (backtick < 0)
            return LastSegment(typeName);

        var argumentsStart = typeName.IndexOf('[', backtick);
        var name = LastSegment(typeName[..backtick]);
        if (argumentsStart < 0 || !typeName.EndsWith(']'))
            return name;

        var arguments = SplitTopLevel(typeName[(argumentsStart + 1)..^1])
            .Select(argument => Short(argument.Trim().TrimStart('[').TrimEnd(']')));

        return $"{name}<{string.Join(", ", arguments)}>";
    }

    /// <summary>
    /// The full name of the type without its assembly, such as <c>System.Boolean</c>.
    /// </summary>
    public static string FullName(string assemblyQualifiedName)
        => assemblyQualifiedName[..TopLevelIndexOf(assemblyQualifiedName, ',', 0)].Trim();

    /// <summary>
    /// Whether an assembly-qualified type name names <paramref name="type"/>, whatever version of its assembly it names.
    /// </summary>
    public static bool Is(string assemblyQualifiedName, Type type)
        => FullName(assemblyQualifiedName) == type.FullName;

    private static string LastSegment(string typeName)
        => typeName[(typeName.LastIndexOfAny(['.', '+']) + 1)..];

    private static IEnumerable<string> SplitTopLevel(string value)
    {
        var start = 0;
        while (start < value.Length)
        {
            var end = TopLevelIndexOf(value, ',', start);
            yield return value[start..end];
            start = end + 1;
        }
    }

    /// <summary>
    /// Index of <paramref name="separator"/> outside of square brackets, or the length of <paramref name="value"/>.
    /// </summary>
    private static int TopLevelIndexOf(string value, char separator, int start)
    {
        var depth = 0;
        for (var i = start; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                default:
                    if (value[i] == separator && depth == 0)
                        return i;
                    break;
            }
        }

        return value.Length;
    }
}
