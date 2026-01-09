using Semver;

namespace ClusterManagement.Backend.Migrations;

internal interface IMigration<T>
{
    SemVersion Version { get; }
    T Execute(T startParameter);
}
