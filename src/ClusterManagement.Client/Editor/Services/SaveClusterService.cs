using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;
using ViciOne.Cluster.Builder.Abstractions;

namespace ClusterManagement.Client.Editor.Services;

internal sealed class SaveClusterService : DialogServiceBase, IDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();

    private readonly IClusterService _clusterService;
    private readonly ILayoutService _layoutService;
    private readonly ILogger<SaveClusterService> _logger;
    private Guid? _currentClusterId;
    private bool _isWorking;
    private SaveConfirmationInfo? _confirmationMessage;
    private IClusterBuilder? _builder;
    private string? _name;
    private string? _description;

    public event Action? ClusterSynchronized;

    public bool IsWorking
    {
        get => _isWorking;
        private set => SetField(ref _isWorking, value);
    }

    public SaveConfirmationInfo? ConfirmationMessage
    {
        get => _confirmationMessage;
        set => SetField(ref _confirmationMessage, value);
    }

    public IClusterBuilder? Builder
    {
        get => _builder;
        private set => SetField(ref _builder, value);
    }

    public string? Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public ClusterStoreOptions StoreOptions { get; } = new();



    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "this notation is clearer")]
    public SaveClusterService(IClusterService clusterService, ILayoutService layoutService, ILogger<SaveClusterService> logger)
    {
        _clusterService = clusterService;
        _layoutService = layoutService;
        _logger = logger;
        _clusterService.ClusterChanged += ClusterService_ClusterChanged;
        _clusterService.CommitClusterChangeFailed += ClusterService_CommitClusterChangeFailed;
    }

    public void Dispose()
    {
        _clusterService.ClusterChanged -= ClusterService_ClusterChanged;
        _clusterService.CommitClusterChangeFailed -= ClusterService_CommitClusterChangeFailed;

        try
        {
            _tokenSource.Cancel();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }
    }

    /// <summary>
    /// handle save request from editor
    /// </summary>
    /// <param name="builder"></param>
    public Task SaveRequested(IClusterBuilder builder)
    {
        try
        {
            Builder = builder;

            Name = builder.Cluster.Name;
            Description = builder.Cluster.Description;
            ConfirmationMessage = null;

            DisplayDialog = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(SaveRequested));
        }

        return Task.CompletedTask;
    }

    public async Task Save()
    {
        try
        {
            if (Builder is null)
                throw new InvalidOperationException("Builder is not initialized");

            SetIsWorking(true);

            // keep id to filter add/error events
            _currentClusterId = Builder.Cluster.Id;

            if (!string.IsNullOrEmpty(Name))
                Builder.Editors.Cluster.SetName(Name);

            if (!string.IsNullOrEmpty(Description))
                Builder.Editors.Cluster.SetDescription(Description);

            var publishOptions = new CommitClusterOptions
            {
                CleanupDependencies = StoreOptions.CleanupDependencies,
            };

            // cluster will be sent to backend and a add notification gets triggered
            var result = await _clusterService.CommitChange(Builder.Cluster, publishOptions, _tokenSource.Token).ConfigureAwait(false);
            if (result is ClusterServiceErrorResult errorResult)
                throw new InvalidOperationException($"Failed to save cluster: {errorResult.ErrorMessage} (Err:{errorResult.ErrorCode})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(Save));
            SetIsWorking(false);

            ConfirmationMessage = new SaveConfirmationInfo
            {
                IsError = true,
                Message = ex.Message,
            };
        }
    }

    private Task ClusterService_ClusterChanged(ClusterChanged change, CancellationToken cancellationToken)
    {
        if (!IsCurrentCluster(change.ClusterId))
            return Task.CompletedTask;

        _logger.LogInformation("Handle {Event} version={Version} clusterId={ClusterId}",
            nameof(ClusterChanged),
            change.Version,
            change.ClusterId);

        SetIsWorking(false);
        DisplayDialog = false;

        if (change.Version is not null)
        {
            Builder?.Cluster.Version = change.Version;
            Builder?.Cluster.VersionCreated = DateTime.UtcNow;
        }
        ClusterSynchronized?.Invoke();

        return Task.CompletedTask;
    }

    private Task ClusterService_CommitClusterChangeFailed(CommitClusterChangeFailed change, CancellationToken cancellationToken)
    {
        if (!IsCurrentCluster(change.ClusterId))
            return Task.CompletedTask;

        _logger.LogInformation("Error {Event} clusterId={ClusterId}", nameof(ClusterError), change.ClusterId);

        ConfirmationMessage = new SaveConfirmationInfo
        {
            IsError = true,
            Message = $"{change.Error.Message} (Err:{change.Error.ErrorCode})"
        };

        SetIsWorking(false);
        DisplayDialog = true;

        return Task.CompletedTask;
    }

    private bool IsCurrentCluster(Guid? clusterId)
        => Equals(_currentClusterId, clusterId);

    private void SetIsWorking(bool isWorking)
        => _layoutService.IsLoadingOverlayVisible = IsWorking = isWorking;
}
