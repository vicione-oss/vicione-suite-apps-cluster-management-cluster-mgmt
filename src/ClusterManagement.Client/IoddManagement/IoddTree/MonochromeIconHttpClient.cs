using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace ClusterManagement.Client.IoddManagement.IoddTree;

internal sealed class MonochromeIconHttpClient
{
    private readonly string _contentIconBasePath = $"_content/{typeof(IoddTreeAdapter).Assembly.GetName().Name}/icons/monochrome-icons";
    private readonly ILocalHttpClient _httpClient;
    private readonly ILogger<MonochromeIconHttpClient> _logger;

    public MonochromeIconHttpClient(ILocalHttpClient httpClient, ILogger<MonochromeIconHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> GetIconSvgAsync(MonochromeIconName iconName)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase - no normalisation but a filename!
        var iconPath = $"{_contentIconBasePath}/32x32/{iconName.GetName().ToLowerInvariant()}.svg";
#pragma warning restore CA1308 // Normalize strings to uppercase

        using var request = new HttpRequestMessage(HttpMethod.Get, iconPath);

        try
        {
            var response = await _httpClient.Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load icon {IconName} from path {IconPath}", iconName, iconPath);
        }

        return null;
    }
}
