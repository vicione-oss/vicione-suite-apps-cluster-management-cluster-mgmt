using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

[SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public sealed record GetActiveClusterResponse : IResponse
{
    public ErrorInfo? RequestError { get; init; }

    public byte[]? CompressedCluster { get; init; }
}
