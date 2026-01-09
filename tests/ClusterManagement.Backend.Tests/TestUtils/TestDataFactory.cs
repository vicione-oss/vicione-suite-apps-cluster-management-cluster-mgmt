using ClusterManagement.Public.Designs;
using NSubstitute;

namespace ClusterManagement.Backend.Tests.TestUtils;

public static class TestDataFactory
{
    public static IDesignProvider SetupMqttDataPortDesignProvider()
    {
        var provider = Substitute.For<IDesignProvider>();
        provider.GetClusterDependencies().Returns(
        [
            new()
            {
                Name = typeof(ViciOne.Suite.System.DataPort).Assembly.GetName().Name!,
                Version = new Version(1, 1, 0)
            },
            new()
            {
                Name = "ViciOne.Suite.DataPort.Mqtt",
                Version = new Version(1, 2, 0)
            },
        ]);

        return provider;
    }
}
