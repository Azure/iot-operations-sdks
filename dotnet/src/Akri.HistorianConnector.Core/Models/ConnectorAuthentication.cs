// <copyright file="ConnectorAuthentication.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.HistorianConnector.Core.Models;

/// <summary>
/// Represents authentication information for connecting to inbound endpoints.
/// </summary>
public sealed record ConnectorAuthentication
{
    /// <summary>
    /// Gets the authentication kind.
    /// </summary>
    public required ConnectorAuthenticationKind Kind { get; init; }

    /// <summary>
    /// Gets the username for username/password authentication.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the password for username/password authentication.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the certificate content for X.509 authentication.
    /// </summary>
    public string? Certificate { get; init; }

    /// <summary>
    /// Gets the private key for X.509 authentication.
    /// </summary>
    public string? PrivateKey { get; init; }

    /// <summary>
    /// Returns an anonymous authentication instance.
    /// </summary>
    public static ConnectorAuthentication Anonymous { get; } = new()
    {
        Kind = ConnectorAuthenticationKind.Anonymous
    };

    /// <summary>
    /// Creates a username/password authentication instance.
    /// </summary>
    public static ConnectorAuthentication UsernamePassword(string? username, string? password) => new()
    {
        Kind = ConnectorAuthenticationKind.UsernamePassword,
        Username = username,
        Password = password
    };

    /// <summary>
    /// Creates an X.509 authentication instance.
    /// </summary>
    public static ConnectorAuthentication X509(string? certificate, string? privateKey) => new()
    {
        Kind = ConnectorAuthenticationKind.X509,
        Certificate = certificate,
        PrivateKey = privateKey
    };
}

/// <summary>
/// Defines supported authentication kinds for inbound endpoints.
/// </summary>
public enum ConnectorAuthenticationKind
{
    /// <summary>
    /// Anonymous authentication.
    /// </summary>
    Anonymous,

    /// <summary>
    /// Username/password authentication.
    /// </summary>
    UsernamePassword,

    /// <summary>
    /// X.509 certificate authentication.
    /// </summary>
    X509
}
