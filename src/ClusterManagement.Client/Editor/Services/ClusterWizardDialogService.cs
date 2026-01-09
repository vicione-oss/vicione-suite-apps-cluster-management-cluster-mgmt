using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Builder;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

internal sealed class ClusterWizardDialogService(IServiceProvider services, ILogger<ClusterWizardDialogService> logger) : DialogServiceBase
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ClusterWizardDialogService> _logger = logger;
    private ClusterBuilder? _builder;

    public IClusterWizard? CurrentWizard { get; private set; }
    public bool CanProcess => !IsProcessing;
    public bool IsProcessing { get; set; }

    public async Task OpenDialog<TWizard>(ClusterBuilder? builder)
        where TWizard : class, IClusterWizard
    {
        try
        {
            CurrentWizard = _services.GetRequiredService<TWizard>();

            _builder = builder;
            if (_builder is null)
                throw new InvalidOperationException("builder is not initialized");

            await CurrentWizard.PrepareWizardOptions(_builder);

            DisplayDialog = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(OpenDialog));
        }
    }

    public async Task ProcessCurrentWizard()
    {
        try
        {
            if (_builder is null)
                throw new InvalidOperationException("builder is not initialized");

            if (CurrentWizard is null)
                throw new InvalidOperationException("wizard is not initialized");

            IsProcessing = true;
            var changed = false;
            _builder.ClusterChanged += (sender, e) => { changed = true; };

            await CurrentWizard.ProcessWizardOptions(_builder);

            if (changed)
            {
                var dataService = _services.GetRequiredService<IDataManagementService>();
                await dataService.ForceRootContainerReload();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(ProcessCurrentWizard));
        }
        finally
        {
            DisplayDialog = false;
            IsProcessing = false;
        }
    }
}
