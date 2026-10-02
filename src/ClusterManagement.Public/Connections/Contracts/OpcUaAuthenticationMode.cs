namespace ClusterManagement.Public.Connections.Contracts;

/// <summary>
/// Specifies how a user identity is authenticated against an OPC UA server session.
/// </summary>
/// <remarks>
/// Mirrors the <c>UserAuthenticationType</c> property of the OPC-UA Client and Server DataPorts, which
/// only support anonymous and username/password ("Basic") session authentication.
/// </remarks>
public enum OpcUaAuthenticationMode
{
    /// <summary>
    /// The client connects without providing user identity information.
    /// </summary>
    Anonymous,

    /// <summary>
    /// The client authenticates using a username and password.
    /// </summary>
    UsernamePassword
}
