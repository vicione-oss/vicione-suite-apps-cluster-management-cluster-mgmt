using ClusterManagement.Client.Connections;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaConnectionComboBoxItemsTests
{
    [Fact]
    public void Should_offer_each_named_network_interface_once_in_order()
    {
        var items = OpcUaConnectionComboBoxItems.NetworkInterfaces(["lan2", "lan1", "lan2"]);

        items.Select(item => item.Value).Should().Equal("lan1", "lan2");
        items.Should().AllSatisfy(item => item.Text.Should().Be(item.Value));
    }

    [Fact]
    public void Should_not_offer_an_empty_network_interface()
    {
        var items = OpcUaConnectionComboBoxItems.NetworkInterfaces(["", " ", "lan1"]);

        items.Select(item => item.Value).Should().Equal("lan1");
    }
}
