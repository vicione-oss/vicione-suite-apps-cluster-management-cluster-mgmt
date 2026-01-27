using Microsoft.AspNetCore.Components.Forms;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;

namespace ClusterManagement.Client.Extensions;

internal static class IMessageBannerServiceExtensions
{
    private const long EmptyZip = 22;

    internal static bool IsValidZipArchive(this IMessageBannerService messageBannerService, InputFileChangeEventArgs args)
    {
        if (args.FileCount != 1)
        {
            messageBannerService.ShowMessageBanner(MessageType.Error, Localization.Common.UploadOnlySingleFile);
            return false;
        }
        if (args.File.Size > ControlPanelConstants.TwoMB)
        {
            messageBannerService.ShowMessageBanner(MessageType.Error, Localization.Common.MaxFileSizeReached);
            return false;
        }
        if (args.File.Size <= EmptyZip)
        {
            messageBannerService.ShowMessageBanner(MessageType.Error, Localization.Common.FileIsEmpty);
            return false;
        }
        if (!args.File.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            messageBannerService.ShowMessageBanner(MessageType.Error, Localization.Common.FileTypeMissmatch);
            return false;
        }

        return true;
    }
}
