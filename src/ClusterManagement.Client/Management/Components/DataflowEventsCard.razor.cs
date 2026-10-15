using System.Globalization;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class DataflowEventsCard
{
    private readonly string _openIconCssClass = MonochromeIconName.ArrowNavigationLightRight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();

    private Version? _loadedVersion;
    private (int Total, int Writable)? _counts;
    private bool _hasFailed;

    // Reads the events itself rather than through the events page's service, so counting does not replace the
    // events, values and subscriptions that page holds.
    [Inject] private IDataflowEventBroker EventBroker { get; set; } = default!;
    [Inject] private ILogger<DataflowEventsCard> Logger { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    /// The running cluster version; its event datapoints are counted again when it changes.
    /// </summary>
    [Parameter, EditorRequired] public Version ActiveVersion { get; set; } = default!;

    /// <summary>
    /// Adds how many of the event datapoints can be written.
    /// </summary>
    [Parameter] public bool Expert { get; set; }

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

            if (counts.Total == 0)
                return ClusterOverview.NoDataflowEvents;

            return Expert
                ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.DataflowEventCount, counts.Total, counts.Writable)
                : string.Format(CultureInfo.CurrentCulture, ClusterOverview.DataflowEventTotal, counts.Total);
        }
    }

    private void OpenEventViewer()
        => Navigation.NavigateToManagementEvents();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to count the dataflow events of version {Version}")]
    private static partial void LogCountFailed(ILogger logger, Exception exception, Version version);
}
