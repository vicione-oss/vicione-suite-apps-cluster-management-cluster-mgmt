using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterArchiveResponse(IEnumerable<byte> ArchiveRaw, ErrorInfo? RequestError = null) : IResponse;
