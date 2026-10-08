using System.Globalization;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class DataflowEventsCard
{
    private Version? _loadedVersion;
    private (int Total, int Writable)? _counts;
    private bool _hasFailed;

    // Reads the events itself rather than through the events page's service, so counting does not replace the
    // events, values and subscriptions that page holds.
    [Inject] private IDataflowEventBroker EventBroker { get; set; } = default!;
    [Inject] private ILogger<DataflowEventsCard> Logger { get; set; } = default!;

    /// <summary>
    /// The running cluster version; its event datapoints are counted again when it changes.
    /// </summary>
    [Parameter, EditorRequired] public Version ActiveVersion { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (ActiveVersion == _loadedVersion)
            return;

        _loadedVersion = ActiveVersion;
        try
        {
            var events = await EventBroker.GetEvents();
            _counts = (events.Count, events.Count(e => e.CanWrite));
            _hasFailed = false;
        }
        catch (Exception e)
        {
            LogCountFailed(Logger, e, ActiveVersion);
            _hasFailed = true;
        }
    }

    private string Summary
    {
        get
        {
            if (_hasFailed)
                return "–";

            if (_counts is not { } counts)
                return "…";

            return counts.Total == 0
                ? ClusterOverview.NoDataflowEvents
                : string.Format(CultureInfo.CurrentCulture, ClusterOverview.DataflowEventCount, counts.Total, counts.Writable);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to count the dataflow events of version {Version}")]
    private static partial void LogCountFailed(ILogger logger, Exception exception, Version version);
}
