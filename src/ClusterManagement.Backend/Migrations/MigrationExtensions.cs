using Semver;

namespace ClusterManagement.Backend.Migrations;

internal static class MigrationExtensions
{
    internal static T Execute<T>(this IReadOnlyCollection<IMigration<T>> migrations, T instance, SemVersion targetVersion)
    {
        var necessaryMigrations = DetermineNecessaryMigrations(targetVersion, migrations);
        try
        {
            return ExecuteMigrations(necessaryMigrations, instance);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not migrate {typeof(T)} to version {targetVersion}.", ex);
        }
    }

    private static IEnumerable<IMigration<T>> DetermineNecessaryMigrations<T>(SemVersion to, IReadOnlyCollection<IMigration<T>> migrations)
        => migrations.OrderByDescending(m => m.Version, SemVersion.SortOrderComparer).TakeWhile(m => to.CompareSortOrderTo(m.Version) <= 0);

    private static T ExecuteMigrations<T>(IEnumerable<IMigration<T>> migrations, T instance)
    {
        var migratedStartParameter = instance;
        foreach (var migration in migrations)
            migratedStartParameter = migration.Execute(migratedStartParameter);
        return migratedStartParameter;
    }
}
