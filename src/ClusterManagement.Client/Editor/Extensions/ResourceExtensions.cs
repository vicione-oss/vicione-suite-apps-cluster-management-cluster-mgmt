using Sdk.Client.Contracts;
using Sdk.Client.Extensions;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class ResourceExtensions
{
    private const string Bundle = "DFE";

    /// <summary>
    /// TODO: we need to ensure that this stays sync with ClusterEditor usages.
    /// </summary>
    /// <param name="resources"></param>
    /// <returns></returns>
    public static List<Resource> AddClusterEditorResources(this List<Resource> resources)
    {
        var editorAssembly = typeof(ViciOne.Ui.ClusterEditor.Components.ClusterEditor).Assembly;

        // editor js bundle
        resources.AddComponentScript(Bundle, "scripts/vicione.cluster-editor.min.js", editorAssembly);

        var zblazorJs = new UriBuilder().WithPath("/_content/Z.Blazor.Diagrams/script.min.js").Uri;
        var zblazorCss = new UriBuilder().WithPath("/_content/Z.Blazor.Diagrams/style.min.css").Uri;
        resources.AddScript(Bundle, zblazorJs);
        resources.AddStylesheet(Bundle, zblazorCss);

        var editorCss = new UriBuilder().WithPath("/_content/ViciOne.Ui.ClusterEditor/css/cluster-editor.css").Uri;
        var editorMdeCss = new UriBuilder().WithPath("/_content/ViciOne.Ui.ClusterEditor/css/easymde.min.css").Uri;
        resources.AddStylesheet(Bundle, editorCss);
        resources.AddStylesheet(Bundle, editorMdeCss);

        return resources;
    }
}
