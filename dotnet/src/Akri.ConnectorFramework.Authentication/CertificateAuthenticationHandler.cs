// <copyright file="CertificateAuthenticationHandler.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Configures an <see cref="HttpClientHandler"/> with TLS client certificate authentication.
/// </summary>
/// <remarks>
/// <para>
/// This is not a DelegatingHandler since TLS client certificates must be configured
/// at the HttpClientHandler level before establishing the connection.
/// </para>
/// <para>Certificate passwords are never logged.</para>
/// </remarks>
public static class CertificateAuthenticationHandler
{
    /// <summary>
    /// Configures the provided <see cref="HttpClientHandler"/> with a client certificate.
    /// </summary>
    /// <param name="handler">The HTTP client handler to configure.</param>
    /// <param name="certificatePath">The file path to the certificate (PFX/PKCS#12 format).</param>
    /// <param name="certificatePassword">The password for the certificate private key (optional).</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when handler or certificatePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the certificate file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the certificate is invalid or expired.</exception>
    public static void ConfigureClientCertificate(
        HttpClientHandler handler,
        string certificatePath,
        string? certificatePassword,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(certificatePath);
        ArgumentNullException.ThrowIfNull(logger);

        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException(
                $"Client certificate file not found: {certificatePath}",
                certificatePath);
        }

        logger.LogDebug("Loading client certificate from {CertificatePath}", certificatePath);

        X509Certificate2? certificate = null;
        try
        {
            certificate = string.IsNullOrEmpty(certificatePassword)
                ? X509CertificateLoader.LoadCertificateFromFile(certificatePath)
                : X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);

            // Validate certificate is not expired
            if (certificate.NotAfter < DateTimeOffset.UtcNow)
            {
                logger.LogError(
                    "Client certificate has expired. NotAfter: {NotAfter:O}",
                    certificate.NotAfter);
                throw new InvalidOperationException(
                    $"Client certificate has expired. Expiry date: {certificate.NotAfter:O}");
            }

            // Validate certificate is currently valid (not before start date)
            if (certificate.NotBefore > DateTimeOffset.UtcNow)
            {
                logger.LogError(
                    "Client certificate is not yet valid. NotBefore: {NotBefore:O}",
                    certificate.NotBefore);
                throw new InvalidOperationException(
                    $"Client certificate is not yet valid. Start date: {certificate.NotBefore:O}");
            }

            // Log certificate details (without sensitive data)
            logger.LogInformation(
                "Client certificate loaded: Subject={Subject}, Thumbprint={Thumbprint}, Expires={NotAfter:O}",
                certificate.Subject,
                certificate.Thumbprint,
                certificate.NotAfter);

            // Add the certificate to the handler
            handler.ClientCertificates.Add(certificate);
            certificate = null; // Ownership transferred to handler
        }
        catch (Exception ex)
        {
            certificate?.Dispose();
            if (ex is InvalidOperationException)
            {
                throw;
            }

            logger.LogError(ex, "Failed to load client certificate from {CertificatePath}", certificatePath);
            throw new InvalidOperationException(
                $"Failed to load client certificate: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Creates a new <see cref="HttpClientHandler"/> configured with a client certificate.
    /// </summary>
    /// <param name="certificatePath">The file path to the certificate (PFX/PKCS#12 format).</param>
    /// <param name="certificatePassword">The password for the certificate private key (optional).</param>
    /// <param name="validateServerCertificate">Whether to validate the server's SSL certificate.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A configured <see cref="HttpClientHandler"/>.</returns>
    public static HttpClientHandler CreateHandler(
        string certificatePath,
        string? certificatePassword,
        bool validateServerCertificate,
        ILogger logger)
    {
        var handler = new HttpClientHandler();

        // Configure server certificate validation
        if (!validateServerCertificate)
        {
            logger.LogWarning("Server certificate validation is disabled. This should only be used for development.");
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        // Configure client certificate
        ConfigureClientCertificate(handler, certificatePath, certificatePassword, logger);

        return handler;
    }
}
