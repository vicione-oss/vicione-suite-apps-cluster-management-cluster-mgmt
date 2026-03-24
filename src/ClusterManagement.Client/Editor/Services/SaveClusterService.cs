using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using ViciOne.Cluster.Builder.Abstractions;

namespace ClusterManagement.Client.Editor.Services;

internal sealed class SaveClusterService : DialogServiceBase,
    IEventConsumer<ClusterChanged>,
    IEventConsumer<CommitClusterChangeFailed>,
    IDisposable
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
    private Guid? _currentCorrelationId;

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

    public event EventHandler<ClusterChanged>? ClusterChanged;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "this notation is clearer")]
    public SaveClusterService(IClusterService clusterService, ILayoutService layoutService, ILogger<SaveClusterService> logger)
    {
        _clusterService = clusterService;
        _layoutService = layoutService;
        _logger = logger;
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
            _currentCorrelationId = await _clusterService.CommitChange(Builder.Cluster, publishOptions, _tokenSource.Token).ConfigureAwait(false);
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

    public Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
    {
        if (!IsCurrentCluster(context.Message.ClusterId))
            return Task.CompletedTask;

        _logger.LogInformation("Handle {Event} version={Version} clusterId={ClusterId}",
            nameof(Public.Events.ClusterChanged),
            context.Message.Version,
            context.Message.ClusterId);

        SetIsWorking(false);
        DisplayDialog = false;

        if (context.CorrelationId == _currentCorrelationId)
        {
            if (context.Message.Version is not null)
            {
                Builder?.Cluster.Version = context.Message.Version;
                Builder?.Cluster.VersionCreated = DateTime.UtcNow;
            }
        }
        else
        {
            ClusterChanged?.Invoke(this, context.Message);
        }

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CommitClusterChangeFailed> context, CancellationToken cancellationToken)
    {
        if (!IsCurrentCluster(context.Message.ClusterId))
            return Task.CompletedTask;

        _logger.LogInformation("Error {Event} clusterId={ClusterId}", nameof(ClusterError), context.Message.ClusterId);

        ConfirmationMessage = new SaveConfirmationInfo
        {
            IsError = true,
            Message = $"{context.Message.Error.Message} (Err:{context.Message.Error.ErrorCode})"
        };

        SetIsWorking(false);
        DisplayDialog = true;

        return Task.CompletedTask;
    }

    private bool IsCurrentCluster(Guid? clusterId)
        => Equals(_currentClusterId, clusterId);

    private void SetIsWorking(bool isWorking)
        => _layoutService.IsLoadingOverlayVisible = IsWorking = isWorking;

    public void Dispose()
    {
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
}
