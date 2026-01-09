namespace ClusterManagement.Client.Editor.Models;

internal sealed record SaveConfirmationInfo
{
    public bool IsError { get; init; }
    public string? Message { get; init; }   // later key for translation
}
