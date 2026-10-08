using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Client.Tests;

internal static class ClientTimeProviderExtensions
{
    /// <summary>
    /// Registers the client time provider the components read the user's time zone and the current time from;
    /// UTC and the system clock unless <paramref name="timeZone"/> or <paramref name="now"/> is given.
    /// </summary>
    public static IServiceCollection AddClientTimeProvider(this IServiceCollection services, TimeZoneInfo? timeZone = null, DateTimeOffset? now = null)
        => services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new ZonedTimeProvider(timeZone ?? TimeZoneInfo.Utc, now));

    private sealed class ZonedTimeProvider(TimeZoneInfo timeZone, DateTimeOffset? now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => timeZone;

        public override DateTimeOffset GetUtcNow() => now ?? base.GetUtcNow();
    }
}
