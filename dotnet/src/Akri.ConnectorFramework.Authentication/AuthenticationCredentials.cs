// <copyright file="AuthenticationCredentials.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Authentication credentials for historian connections.
/// Validation rules enforce that required fields are present based on <see cref="AuthMethod"/>.
/// </summary>
/// <remarks>
/// <para>Secrets (Password, ClientSecret, CertificatePassword) must never appear in logs.</para>
/// <para>These values are typically loaded from environment variables or mounted secret files.</para>
/// </remarks>
public sealed class AuthenticationCredentials : IValidatableObject
{
    /// <summary>
    /// Gets or sets the authentication method to use.
    /// </summary>
    [Required]
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Basic;

    // ─────────────────────────────────────────────────────────────────────────
    // Basic Authentication
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the username for Basic authentication.
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.Basic"/>.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for Basic authentication.
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.Basic"/>.
    /// </summary>
    /// <remarks>Never log this value.</remarks>
    public string? Password { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // OAuth Bearer Token Authentication
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the OAuth token endpoint URL.
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.OAuth"/>.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client ID.
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.OAuth"/>.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.OAuth"/>.
    /// </summary>
    /// <remarks>Never log this value.</remarks>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth scopes (space-separated).
    /// Optional; defaults to empty.
    /// </summary>
    public string? Scopes { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // TLS Client Certificate Authentication
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the file path to the client certificate (PFX/PKCS#12 format).
    /// Required when <see cref="AuthMethod"/> is <see cref="Models.AuthMethod.Certificate"/>.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the password for the certificate private key.
    /// Required if the certificate is password-protected.
    /// </summary>
    /// <remarks>Never log this value.</remarks>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Validates that the required fields are present based on the selected <see cref="AuthMethod"/>.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (AuthMethod)
        {
            case AuthMethod.Basic:
                if (string.IsNullOrWhiteSpace(Username))
                {
                    yield return new ValidationResult(
                        "Username is required for Basic authentication.",
                        [nameof(Username)]);
                }

                if (string.IsNullOrWhiteSpace(Password))
                {
                    yield return new ValidationResult(
                        "Password is required for Basic authentication.",
                        [nameof(Password)]);
                }

                break;

            case AuthMethod.OAuth:
                if (string.IsNullOrWhiteSpace(TokenEndpoint))
                {
                    yield return new ValidationResult(
                        "TokenEndpoint is required for OAuth authentication.",
                        [nameof(TokenEndpoint)]);
                }

                if (string.IsNullOrWhiteSpace(ClientId))
                {
                    yield return new ValidationResult(
                        "ClientId is required for OAuth authentication.",
                        [nameof(ClientId)]);
                }

                if (string.IsNullOrWhiteSpace(ClientSecret))
                {
                    yield return new ValidationResult(
                        "ClientSecret is required for OAuth authentication.",
                        [nameof(ClientSecret)]);
                }

                break;

            case AuthMethod.Certificate:
                if (string.IsNullOrWhiteSpace(CertificatePath))
                {
                    yield return new ValidationResult(
                        "CertificatePath is required for Certificate authentication.",
                        [nameof(CertificatePath)]);
                }

                break;

            default:
                yield return new ValidationResult(
                    $"Unknown authentication method: {AuthMethod}",
                    [nameof(AuthMethod)]);
                break;
        }
    }
}
