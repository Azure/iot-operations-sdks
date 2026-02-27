// <copyright file="BasicAuthenticationHandler.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Net.Http.Headers;
using System.Text;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// HTTP message handler that adds Basic authentication to outgoing requests.
/// </summary>
/// <remarks>
/// <para>
/// Implements the DelegatingHandler pattern to inject the Authorization header
/// with Base64-encoded credentials into all outgoing HTTP requests.
/// </para>
/// <para>Credentials are never logged.</para>
/// </remarks>
public sealed class BasicAuthenticationHandler : DelegatingHandler
{
    private readonly AuthenticationHeaderValue _authHeader;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="username">The username for Basic authentication.</param>
    /// <param name="password">The password for Basic authentication.</param>
    /// <exception cref="ArgumentNullException">Thrown when username or password is null.</exception>
    public BasicAuthenticationHandler(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        var credentials = $"{username}:{password}";
        var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        _authHeader = new AuthenticationHeaderValue("Basic", encodedCredentials);
    }

    /// <summary>
    /// Sends an HTTP request with Basic authentication header.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Set the Authorization header for Basic auth
        request.Headers.Authorization = _authHeader;

        return base.SendAsync(request, cancellationToken);
    }
}
