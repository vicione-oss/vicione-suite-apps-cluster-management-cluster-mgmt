using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace ClusterManagement.Client.Tests.Services;

internal sealed class StaticWebAssetsRuntimeJsonPathProvider : IStaticWebAssetsRuntimeJsonPathProvider
{
    public string GetPath()
        => AppContext.BaseDirectory;
}
