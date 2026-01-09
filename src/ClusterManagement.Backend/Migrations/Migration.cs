using Semver;

namespace ClusterManagement.Backend.Migrations;

internal sealed record Migration<TBase, TFrom, TTo>(SemVersion Version, Func<TFrom, TTo> Action) : IMigration<TBase>
    where TBase : notnull
    where TFrom : TBase
    where TTo : TBase
{
    public TBase Execute(TBase instance) => Action((TFrom)instance);
}
