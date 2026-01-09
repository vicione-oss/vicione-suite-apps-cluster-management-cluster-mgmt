using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class MarkupStringExtensions
{
    public static MarkupString WithHighlightedText(this MarkupString str, string? text, StringComparison comparison = StringComparison.InvariantCulture)
    {
        if (string.IsNullOrWhiteSpace(text) || !str.Value.Contains(text, comparison))
        {
            return str;
        }

        // Todo: fix multiple different case matches? will not realy be relevant in this usecase
        var index = str.Value.IndexOf(text, comparison);
        var actualCaseSensitiveSubstring = str.Value.Substring(index, text.Length);

        return new MarkupString(str.ToString().Replace(text, $"<b>{actualCaseSensitiveSubstring}</b>", comparison));
    }
}
