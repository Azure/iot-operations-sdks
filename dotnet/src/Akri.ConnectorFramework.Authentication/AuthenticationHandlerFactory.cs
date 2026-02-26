// <copyright file="AuthenticationHandlerFactory.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>


using Microsoft.Extensions.Logging;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Factory for creating authentication handlers based on the configured <see cref="AuthMethod"/>.
/// </summary>
public sealed class AuthenticationHandlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationHandlerFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public AuthenticationHandlerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a <see cref="DelegatingHandler"/> configured for the specified authentication method.
    /// </summary>
    /// <param name="credentials">The authentication credentials.</param>
    /// <returns>A configured DelegatingHandler for the auth method, or null for Certificate auth.</returns>
    /// <remarks>
    /// For Certificate authentication, returns null because certificate configuration
    /// must be done at the HttpClientHandler level, not as a DelegatingHandler.
    /// Use <see cref="CreatePrimaryHandler"/> for Certificate auth.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when credentials is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when credentials are invalid.</exception>
    public DelegatingHandler? CreateDelegatingHandler(AuthenticationCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return credentials.AuthMethod switch
        {
            AuthMethod.Basic => CreateBasicHandler(credentials),
            AuthMethod.OAuth => CreateOAuthHandler(credentials),
            AuthMethod.Certificate => null, // Handled at HttpClientHandler level
            _ => throw new InvalidOperationException($"Unknown authentication method: {credentials.AuthMethod}"),
        };
    }

    /// <summary>
    /// Creates an <see cref="HttpClientHandler"/> for the specified authentication method.
    /// </summary>
    /// <param name="credentials">The authentication credentials.</param>
    /// <param name="validateServerCertificate">Whether to validate server certificates.</param>
    /// <returns>A configured HttpClientHandler.</returns>
    /// <remarks>
    /// For Certificate authentication, this returns a handler with the client certificate configured.
    /// For other authentication methods, returns a standard handler.
    /// </remarks>
    public HttpClientHandler CreatePrimaryHandler(
        AuthenticationCredentials credentials,
        bool validateServerCertificate = true)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var handler = new HttpClientHandler();

        // Configure server certificate validation
        if (!validateServerCertificate)
        {
            var logger = _loggerFactory.CreateLogger<AuthenticationHandlerFactory>();
            logger.LogWarning("Server certificate validation is disabled. This should only be used for development.");
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        // For certificate auth, configure the client certificate on the handler
        if (credentials.AuthMethod == AuthMethod.Certificate)
        {
            if (string.IsNullOrWhiteSpace(credentials.CertificatePath))
            {
                throw new InvalidOperationException("CertificatePath is required for Certificate authentication.");
            }

            var logger = _loggerFactory.CreateLogger(typeof(CertificateAuthenticationHandler).FullName!);
            CertificateAuthenticationHandler.ConfigureClientCertificate(
                handler,
                credentials.CertificatePath,
                credentials.CertificatePassword,
                logger);
        }

        return handler;
    }

    /// <summary>
    /// Creates a complete handler chain for the specified connection configuration.
    /// </summary>
    /// <param name="connection">The historian connection configuration.</param>
    /// <returns>The outermost handler in the chain (either auth handler wrapping primary, or just primary).</returns>
    public HttpMessageHandler CreateHandlerChain(HistorianConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var primaryHandler = CreatePrimaryHandler(
            connection.Credentials,
            connection.ValidateServerCertificate);

        var delegatingHandler = CreateDelegatingHandler(connection.Credentials);

        if (delegatingHandler == null)
        {
            // Certificate auth - no delegating handler needed
            return primaryHandler;
        }

        // Chain the delegating handler with the primary handler
        delegatingHandler.InnerHandler = primaryHandler;
        return delegatingHandler;
    }

    private BasicAuthenticationHandler CreateBasicHandler(AuthenticationCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            throw new InvalidOperationException("Username is required for Basic authentication.");
        }

        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new InvalidOperationException("Password is required for Basic authentication.");
        }

        return new BasicAuthenticationHandler(credentials.Username, credentials.Password);
    }

    private OAuthBearerAuthenticationHandler CreateOAuthHandler(AuthenticationCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.TokenEndpoint))
        {
            throw new InvalidOperationException("TokenEndpoint is required for OAuth authentication.");
        }

        if (string.IsNullOrWhiteSpace(credentials.ClientId))
        {
            throw new InvalidOperationException("ClientId is required for OAuth authentication.");
        }

        if (string.IsNullOrWhiteSpace(credentials.ClientSecret))
        {
            throw new InvalidOperationException("ClientSecret is required for OAuth authentication.");
        }

        var logger = _loggerFactory.CreateLogger<OAuthBearerAuthenticationHandler>();

        return new OAuthBearerAuthenticationHandler(
            credentials.TokenEndpoint,
            credentials.ClientId,
            credentials.ClientSecret,
            credentials.Scopes,
            _loggerFactory,
            false, // Default: proactive renewal disabled
            5);    // Default: 5 minute buffer
    }
}
