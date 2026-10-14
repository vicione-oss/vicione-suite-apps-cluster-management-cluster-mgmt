using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Extensions;

namespace ClusterManagement.Client.Connections.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddOpcUaConnections(this IServiceCollection services, string moduleId)
    {
        services.AddStreamUploadHandler<OpcUaCertificateUpload>(moduleId, o =>
        {
            o.PathTransform = p => Path.Combine(p, OpcUaCertificateUpload.DirectoryName);
            // A unique name, so two uploads of the same file don't overwrite or delete each other.
            o.FilenameTransform = _ => $"{Guid.NewGuid():N}.cert";
        });

        return services;
    }
}
