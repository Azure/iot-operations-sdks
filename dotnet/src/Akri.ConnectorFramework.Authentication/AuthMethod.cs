// <copyright file="AuthMethod.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Supported authentication methods for historian connections.
/// </summary>
public enum AuthMethod
{
    /// <summary>
    /// HTTP Basic Authentication (username/password encoded as Base64).
    /// </summary>
    Basic = 0,

    /// <summary>
    /// OAuth 2.0 Bearer Token authentication.
    /// </summary>
    OAuth = 1,

    /// <summary>
    /// TLS Client Certificate (mutual TLS) authentication.
    /// </summary>
    Certificate = 2,
}
