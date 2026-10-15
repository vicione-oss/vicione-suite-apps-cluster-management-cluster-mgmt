using ClusterManagement.Client.Management.Models;

namespace ClusterManagement.Client.Tests.Management;

public class CycleTimeSeriesTests
{
    private static readonly Guid s_source = Guid.NewGuid();
    private static readonly DateTimeOffset s_start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Keeps_the_slowest_cycle_of_each_second()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);

        // Act
        series.Add(s_source, Report(4.0), s_start);
        series.Add(s_source, Report(9.5), s_start.AddMilliseconds(400));
        series.Add(s_source, Report(2.0), s_start.AddMilliseconds(900));

        // Assert
        series.Window(s_source, s_start).Should().Equal(null, null, 9.5);
        series.Latest(s_source, s_start).Should().Be(2.0);
    }

    [Fact]
    public void Window_has_gaps_for_seconds_without_reports()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 4);
        series.Add(s_source, Report(1.0), s_start);
        series.Add(s_source, Report(3.0), s_start.AddSeconds(2));

        // Act
        var window = series.Window(s_source, s_start.AddSeconds(3));

        // Assert
        window.Should().Equal(1.0, null, 3.0, null);
    }

    [Fact]
    public void Drops_seconds_older_than_the_window()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 2);

        // Act
        for (var second = 0; second < 5; second++)
            series.Add(s_source, Report(second), s_start.AddSeconds(second));

        // Assert
        series.Window(s_source, s_start.AddSeconds(4)).Should().Equal(3.0, 4.0);
    }

    [Fact]
    public void A_report_adds_the_chain_under_the_host_and_each_engine_under_its_deployment()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);
        var deployment = Guid.NewGuid();
        var report = new EngineCycleReport(4.8, [new EngineCycleChainLink(deployment, 2.1)]);

        // Act
        series.Add(s_source, report, s_start);

        // Assert
        series.Latest(s_source, s_start).Should().Be(4.8);
        series.Latest(deployment, s_start).Should().Be(2.1);
    }

    [Fact]
    public void Drops_a_source_without_a_report_in_the_window()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);
        var replaced = Guid.NewGuid();
        series.Add(replaced, Report(2.0), s_start);

        // Act
        series.Add(s_source, Report(1.0), s_start.AddSeconds(3));

        // Assert
        series.Latest(s_source, s_start.AddSeconds(3)).Should().Be(1.0);
        series.Latest(replaced, s_start.AddSeconds(3)).Should().BeNull();
    }

    [Fact]
    public void Keeps_a_source_that_reported_within_the_window()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);
        var quiet = Guid.NewGuid();
        series.Add(quiet, Report(2.0), s_start);

        // Act
        series.Add(s_source, Report(1.0), s_start.AddSeconds(2));

        // Assert
        series.Latest(quiet, s_start.AddSeconds(2)).Should().Be(2.0);
    }

    [Fact]
    public void A_source_that_stopped_reporting_has_no_latest_value_even_when_no_other_source_reports()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);
        series.Add(s_source, Report(1.0), s_start);

        // Act
        var latest = series.Latest(s_source, s_start.AddSeconds(3));

        // Assert
        latest.Should().BeNull();
    }

    [Fact]
    public void Last_report_is_the_second_of_the_newest_report()
    {
        // Arrange
        var series = new CycleTimeSeries();
        var source = Guid.NewGuid();
        series.Add(source, new EngineCycleReport(1, []), DateTimeOffset.FromUnixTimeSeconds(100));

        // Act
        series.Add(source, new EngineCycleReport(1, []), DateTimeOffset.FromUnixTimeMilliseconds(103_400));

        // Assert
        series.LastReport(source).Should().Be(DateTimeOffset.FromUnixTimeSeconds(103));
    }

    [Fact]
    public void Range_holds_the_fastest_and_slowest_of_every_report_since_the_first()
    {
        // Arrange
        var series = new CycleTimeSeries();
        var source = Guid.NewGuid();
        series.Add(source, new EngineCycleReport(4, []), DateTimeOffset.FromUnixTimeSeconds(100));
        series.Add(source, new EngineCycleReport(1.5, []), DateTimeOffset.FromUnixTimeMilliseconds(100_500));

        // Act
        series.Add(source, new EngineCycleReport(9, []), DateTimeOffset.FromUnixTimeSeconds(130));

        // Assert
        series.Range(source).Should().Be(new CycleRange(1.5, 9, DateTimeOffset.FromUnixTimeSeconds(100)));
    }

    [Fact]
    public void Range_starts_over_once_the_source_was_silent_for_the_window()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 60);
        var source = Guid.NewGuid();
        series.Add(source, new EngineCycleReport(9, []), DateTimeOffset.FromUnixTimeSeconds(100));

        // Act
        series.Add(source, new EngineCycleReport(2, []), DateTimeOffset.FromUnixTimeSeconds(200));

        // Assert
        series.Range(source).Should().Be(new CycleRange(2, 2, DateTimeOffset.FromUnixTimeSeconds(200)));
    }

    [Fact]
    public void A_source_without_a_report_has_no_range()
        => new CycleTimeSeries().Range(Guid.NewGuid()).Should().BeNull();

    [Fact]
    public void A_source_without_a_report_has_no_last_report()
        => new CycleTimeSeries().LastReport(Guid.NewGuid()).Should().BeNull();

    [Fact]
    public void Clear_drops_every_source()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);
        series.Add(s_source, Report(1.0), s_start);

        // Act
        series.Clear();

        // Assert
        series.Latest(s_source, s_start).Should().BeNull();
        series.Window(s_source, s_start).Should().OnlyContain(v => v == null);
    }

    [Fact]
    public void Unknown_source_has_an_empty_window_and_no_latest_value()
    {
        // Arrange
        var series = new CycleTimeSeries(seconds: 3);

        // Act
        var window = series.Window(Guid.NewGuid(), s_start);

        // Assert
        window.Should().HaveCount(3).And.OnlyContain(v => v == null);
        series.Latest(Guid.NewGuid(), s_start).Should().BeNull();
    }

    private static EngineCycleReport Report(double milliseconds) => new(milliseconds, []);
}
