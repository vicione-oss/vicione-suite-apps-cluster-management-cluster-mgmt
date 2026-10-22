namespace ClusterManagement.Client.Extensions;

/// <summary>
/// What a name shows when hovered.
/// </summary>
internal static class HoverTexts
{
    /// <summary>
    /// <paramref name="name"/>, followed on the next line by <paramref name="description"/> unless it is blank or only
    /// repeats the name. The name stays first, so a name cut off by the layout can still be read in full.
    /// </summary>
    public static string NameWithDescription(string name, string? description)
        => string.IsNullOrWhiteSpace(description) || description.Trim() == name.Trim()
            ? name
            : $"{name}\n{description.Trim()}";
}
