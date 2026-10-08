using ClusterManagement.Public.Enums;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Models;

public sealed class InstanceProgressModel(Guid id, string name, string nodeName)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string NodeName { get; } = nodeName;

    public bool IsStarted { get; private set; }
    public bool IsUpdated { get; private set; }
    public ErrorInfo? Error { get; private set; }

    public bool HasFailed => Error is not null;

    internal void Record(ClusterUpdateChange change, ErrorInfo? error)
    {
        if (error is not null)
        {
            Fail(error);
            return;
        }

        IsStarted |= change == ClusterUpdateChange.Start;
        IsUpdated |= change == ClusterUpdateChange.Update;
    }

    internal void Fail(ErrorInfo error)
    {
        Error = error;
        IsUpdated = false;
    }
}
