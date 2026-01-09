using ClusterManagement.Public.Designs;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

internal sealed class LoadFunctionBlocksService(
    IDesignProvider designProvider,
    IDataManagementService dataManagementService,
    ILogger<LoadFunctionBlocksService> logger) : DialogServiceBase
{
    public IEnumerable<ClusterDependency>? Dependencies { get; private set; }
    public IEnumerable<ClusterDependency>? SelectedDependencies { get; set; }

    public async Task LoadPackageReferences(ClusterDependency[]? preSelectedPackages = null)
    {
        try
        {
            // list of FunctionBlock name and version
            Dependencies = (await designProvider.GetClusterDependencies())
                .Where(k => !k.Name.Contains("DataPort", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k.Name);

            // selected items have to be objects from source
            if (preSelectedPackages is not null)
                SelectedDependencies = Dependencies.Where(k => preSelectedPackages.Any(p => k.Name == p.Name));

            DisplayDialog = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(LoadPackageReferences));
        }
    }

    public async Task LoadSelectedFunctionBlocks()
    {
        try
        {
            if (SelectedDependencies is null || !SelectedDependencies.Any())
                return;

            // list of FunctionBock design ids for the selected dependencies
            var designIds = await designProvider.GetFunctionBlockDesignIds([.. SelectedDependencies]);

            // load them into builder -> editor
            dataManagementService.LoadFunctionBlockDesigns(designIds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, nameof(LoadSelectedFunctionBlocks));
        }
        finally
        {
            DisplayDialog = false;
        }
    }

    public bool HasSelectedPackages()
        => SelectedDependencies is not null && SelectedDependencies.Any();
}
