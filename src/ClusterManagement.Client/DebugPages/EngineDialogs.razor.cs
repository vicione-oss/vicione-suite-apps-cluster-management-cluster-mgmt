using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.Blazor.Components.Dialog.Components;

namespace ClusterManagement.Client.DebugPages;

/// <summary>
/// The dialogs of the Engines page: the packages of an engine, and the question before an engine is torn down.
/// </summary>
public sealed partial class EngineDialogs
{
    private Dialog? _packagesDialog;
    private Dialog? _tearDownDialog;
    private EngineRuntimeModel? _packagesOf;
    private IReadOnlyList<KeyValuePair<string, string>> _packages = [];
    private EngineRuntimeModel? _pendingTearDown;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private ILogger<EngineDialogs> Logger { get; set; } = default!;

    /// <summary>
    /// The engine the user agreed to tear down.
    /// </summary>
    [Parameter] public EventCallback<EngineRuntimeModel> OnTearDownConfirmed { get; set; }

    /// <summary>
    /// Shows the packages the instance of <paramref name="engine"/> holds for it, sorted by name; a banner says why
    /// when they cannot be read.
    /// </summary>
    public async Task ShowPackages(EngineRuntimeModel engine, CancellationToken cancellationToken)
    {
        try
        {
            var response = await Mediator.Request<GetPackagesOnInstanceRequest, GetPackagesOnInstanceResponse>(
                new(engine.EngineId), engine.InstanceId, cancellationToken);
            if (response.RequestError is { } error)
                throw new InvalidOperationException(error.Message);

            _packagesOf = engine;
            _packages = [.. (response.Packages ?? new Dictionary<string, string>()).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)];
            StateHasChanged();
            await _packagesDialog!.ShowAsync();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogPackagesFailed(Logger, e, engine.EngineId);
            MessageBannerService.ShowMessageBanner(MessageType.Error,
                string.Format(CultureInfo.CurrentCulture, ClusterOverview.PackagesNotRead, engine.EngineName, e.Message));
        }
    }

    /// <summary>
    /// Asks whether to tear down <paramref name="engine"/>; a yes raises <see cref="OnTearDownConfirmed"/>.
    /// </summary>
    public async Task AskToTearDown(EngineRuntimeModel engine)
    {
        _pendingTearDown = engine;
        StateHasChanged();
        await _tearDownDialog!.ShowAsync();
    }

    private async Task ClosePackages()
        => await _packagesDialog!.CloseAsync();

    private async Task CancelTearDown()
    {
        _pendingTearDown = null;
        await _tearDownDialog!.CloseAsync();
    }

    private async Task ConfirmTearDown()
    {
        await _tearDownDialog!.CloseAsync();
        if (_pendingTearDown is { } engine)
            await OnTearDownConfirmed.InvokeAsync(engine);

        _pendingTearDown = null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read the packages of engine {EngineId}")]
    private static partial void LogPackagesFailed(ILogger logger, Exception exception, Guid engineId);
}
