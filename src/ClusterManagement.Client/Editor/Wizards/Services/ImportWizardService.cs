using ClusterManagement.Client.Editor.Extensions;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Extensions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Localization.Extensions;

namespace ClusterManagement.Client.Editor.Wizards.Services;

internal sealed partial class ImportWizardService(ILogger<ImportWizardService> logger) : IClusterWizard
{
    private const long MaxClusterSize = 1024L * 1024L * 50;//~50 mb

    private Cluster? _uploadedCluster;
    private Exception? _uploadError;

    public bool IsFileUploaded { get; private set; }
    public string? FileName { get; private set; }
    public string? FileSize { get; private set; }

    public static string MaxFileSize => MaxClusterSize.LocalizeFileSizeHumanReadable();

    public ClusterImportOptions Options { get; set; } = new()
    {
        MergeNodeGroups = true,
        MergeNodes = true,
        MergeApplications = true,
        MergeEngineHosts = true,
    };

    public string Title => "Import Cluster";
    public Type ComponentType => typeof(ImportWizardComponent);

    public Task PrepareWizardOptions(ClusterBuilder builder)
        => Task.CompletedTask;

    public Task ProcessWizardOptions(ClusterBuilder builder)
    {
        if (_uploadedCluster is null)
            throw _uploadError ?? new InvalidOperationException("Uploading cluster failed");

        // TODO: error handling (optional?)
        FileSize = null;
        var result = builder.ImportCluster(_uploadedCluster, Options);

        LogImportInformation(
            logger,
            _uploadedCluster.Name,
            _uploadedCluster.Version,
            result.TotalAdded,
            result.TotalMerged,
            result.TotalSkipped);

        return Task.CompletedTask;
    }

    public async Task UploadClusterJsonFile(InputFileChangeEventArgs obj)
    {
        try
        {
            FileSize = obj.File.Size.LocalizeFileSizeHumanReadable();
            FileName = obj.File.Name;

            _uploadedCluster = await obj.File.DeserializeCluster(MaxClusterSize);
            IsFileUploaded = true;
        }
        catch (Exception e)
        {
            _uploadError = e;
            IsFileUploaded = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported cluster {ClusterName} v{ClusterVersion} results - added:{Added} merged:{Merged} skipped={Skipped}")]
    private static partial void LogImportInformation(ILogger<ImportWizardService> logger, string clusterName, Version clusterVersion, int added, int merged, int skipped);
}
