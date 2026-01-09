using ClusterManagement.Client.Management.Models;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Extensions;

internal static class ClusterUpdateStatusModelExtensions
{
    public static void ProcessNodeChange(this ClusterUpdateStatusModel status, Guid nodeId, ErrorInfo? error)
    {
        var node = status.Nodes.FirstOrDefault(k => k?.Id == nodeId);
        if (node is null) return;

        node.Processed = true;
        node.Error = error;
    }

    public static void ProcessApplicationChange(this ClusterUpdateStatusModel status, Guid applicationId, ErrorInfo? error)
    {
        var node = status.Nodes.Select(k => k.CoreOS).FirstOrDefault(k => k?.Id == applicationId);
        if (node is null) return;

        node.Processed = true;
        node.Error = error;
    }
}
