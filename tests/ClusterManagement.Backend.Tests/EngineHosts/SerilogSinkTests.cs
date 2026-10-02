using System.Text.Json;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Tests.EngineHosts;

public sealed class SerilogSinkTests
{
    public sealed class Journal
    {
        [Fact]
        public void Writes_log_event_properties_as_journal_fields()
        {
            // Arrange
            var sink = SerilogSink.Journal(Guid.NewGuid(), "EngineHost");

            // Act
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(sink, JsonSetup.DefaultOptions));

            // Assert
            json.RootElement.GetProperty("Args").GetProperty("WriteCustomKeyValues").GetBoolean().Should().BeTrue();
        }
    }
}
