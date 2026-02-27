// <copyright file="HistorianConnection.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// Represents the connection configuration to a historian system.
/// </summary>
public sealed class HistorianConnection : IValidatableObject
{
    /// <summary>
    /// Gets or sets the base URL of the historian API endpoint.
    /// Must be a valid HTTPS URL.
    /// </summary>
    /// <example>https://pi-web-api-server/piwebapi</example>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication credentials for this connection.
    /// </summary>
    [Required]
    public AuthenticationCredentials Credentials { get; set; } = new();

    /// <summary>
    /// Gets or sets the HTTP request timeout.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the retry policy configuration.
    /// </summary>
    public RetryPolicyConfig RetryPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to validate the server's SSL certificate.
    /// Defaults to true. Set to false only for development/testing.
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether proactive session renewal is enabled.
    /// When enabled, tokens are refreshed before expiry to prevent failures.
    /// </summary>
    public bool ProactiveRenewalEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the buffer time in minutes before token expiry to trigger renewal.
    /// Only used when ProactiveRenewalEnabled is true.
    /// </summary>
    public int RenewalBufferMinutes { get; set; } = 5;

    /// <summary>
    /// Validates the connection configuration.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate BaseUrl is a valid HTTPS URI
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
            {
                yield return new ValidationResult(
                    "BaseUrl must be a valid absolute URI.",
                    [nameof(BaseUrl)]);
            }
            else if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                yield return new ValidationResult(
                    "BaseUrl must use HTTPS (or HTTP for development only).",
                    [nameof(BaseUrl)]);
            }
        }

        // Validate timeout is reasonable
        if (Timeout <= TimeSpan.Zero)
        {
            yield return new ValidationResult(
                "Timeout must be greater than zero.",
                [nameof(Timeout)]);
        }

        if (Timeout > TimeSpan.FromMinutes(10))
        {
            yield return new ValidationResult(
                "Timeout should not exceed 10 minutes.",
                [nameof(Timeout)]);
        }

        // Validate nested credentials
        if (Credentials != null)
        {
            var credentialResults = Credentials.Validate(
                new ValidationContext(Credentials, validationContext, validationContext.Items));

            foreach (var result in credentialResults)
            {
                yield return new ValidationResult(
                    $"Credentials: {result.ErrorMessage}",
                    result.MemberNames.Select(m => $"{nameof(Credentials)}.{m}").ToArray());
            }
        }

        // Validate retry policy
        if (RetryPolicy != null)
        {
            var retryResults = RetryPolicy.Validate(
                new ValidationContext(RetryPolicy, validationContext, validationContext.Items));

            foreach (var result in retryResults)
            {
                yield return new ValidationResult(
                    $"RetryPolicy: {result.ErrorMessage}",
                    result.MemberNames.Select(m => $"{nameof(RetryPolicy)}.{m}").ToArray());
            }
        }
    }
}
