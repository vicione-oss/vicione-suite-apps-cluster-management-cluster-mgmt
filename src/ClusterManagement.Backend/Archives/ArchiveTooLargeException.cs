using System.Diagnostics.CodeAnalysis;

namespace ClusterManagement.Backend.Archives;

/// <summary>
/// Thrown when an uploaded archive has more entries or extracts to more bytes than allowed. Retrying cannot succeed,
/// so this is treated as a permanent failure.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
public sealed class ArchiveTooLargeException(string message) : Exception(message);
