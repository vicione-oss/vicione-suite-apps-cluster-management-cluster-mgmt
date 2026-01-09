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
        // jquery and dx.all.js come with AddDxSharedResources
        resources.AddComponentScript(Bundle, "scripts/vicione.cluster-editor.min.js", editorAssembly);

        resources.AddScript(Bundle, "/_content/Z.Blazor.Diagrams/script.min.js");
        resources.AddStylesheet(Bundle, "/_content/Z.Blazor.Diagrams/style.min.css");

        return resources;
    }
}
