// <copyright file="OAuthBearerAuthenticationHandler.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Akri.ConnectorFramework.Authentication;

/// <summary>
/// HTTP message handler that adds OAuth 2.0 Bearer token authentication to outgoing requests.
/// </summary>
/// <remarks>
/// <para>
/// Implements the DelegatingHandler pattern to inject the Authorization header
/// with a Bearer token. Handles token caching, expiry detection, and automatic refresh.
/// </para>
/// <para>Tokens and client secrets are never logged.</para>
/// </remarks>
public sealed class OAuthBearerAuthenticationHandler : DelegatingHandler
{
    private readonly string _tokenEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string? _scopes;
    private readonly ILogger<OAuthBearerAuthenticationHandler> _logger;
    private readonly bool _proactiveRenewalEnabled;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly HttpClient _tokenHttpClient;
    private readonly Timer? _renewalTimer;

    private int _disposed;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    /// <summary>
    /// Buffer time before token expiry to trigger refresh.
    /// </summary>
    private readonly TimeSpan _renewalBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthBearerAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="tokenEndpoint">The OAuth token endpoint URL.</param>
    /// <param name="clientId">The OAuth client ID.</param>
    /// <param name="clientSecret">The OAuth client secret.</param>
    /// <param name="scopes">Optional space-separated scopes.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="proactiveRenewalEnabled">Whether proactive renewal is enabled.</param>
    /// <param name="renewalBufferMinutes">Buffer time in minutes before expiry for renewal.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public OAuthBearerAuthenticationHandler(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string? scopes,
        ILoggerFactory loggerFactory,
        bool proactiveRenewalEnabled,
        int renewalBufferMinutes,
        HttpMessageHandler? tokenMessageHandler = null)
    {
        ArgumentNullException.ThrowIfNull(tokenEndpoint);
        ArgumentNullException.ThrowIfNull(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _tokenEndpoint = tokenEndpoint;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scopes = scopes;
        _logger = loggerFactory.CreateLogger<OAuthBearerAuthenticationHandler>();
        _proactiveRenewalEnabled = proactiveRenewalEnabled;
        _renewalBuffer = proactiveRenewalEnabled ? TimeSpan.FromMinutes(renewalBufferMinutes) : TimeSpan.Zero;

        // Create a dedicated HttpClient for token requests (avoids circular dependency)
        _tokenHttpClient = tokenMessageHandler is null
            ? new HttpClient()
            : new HttpClient(tokenMessageHandler);

        // Initialize proactive renewal timer if enabled
        if (_proactiveRenewalEnabled)
        {
            _renewalTimer = new Timer(
                callback: _ => Task.Run(() => ProactiveRenewalCallbackAsync()),
                state: null,
                dueTime: Timeout.Infinite, // Start disabled, will be scheduled after first token acquisition
                period: Timeout.Infinite); // One-shot timer, rescheduled after each renewal
        }
    }

    /// <summary>
    /// Sends an HTTP request with OAuth Bearer token authentication header.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        const int MaxRetries = 3;
        var retryDelay = TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            // Get fresh token for each attempt (will refresh if needed)
            var token = await GetValidTokenAsync(cancellationToken).ConfigureAwait(false);

            // Clone request for this attempt (except on first attempt)
            HttpRequestMessage? clonedRequest = null;
            var attemptRequest = request;

            if (attempt != 0)
            {
                clonedRequest = CloneHttpRequest(request);
                attemptRequest = clonedRequest;
            }
            try
            {
                attemptRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);

                // If successful or not a 401, return the response (caller owns disposal)
                if (response.IsSuccessStatusCode || response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    return response;
                }

                // Dispose the failed response before retrying
                response.Dispose();
            }
            finally
            {
                clonedRequest?.Dispose();
            }

            // If this was the last attempt, don't retry
            if (attempt == MaxRetries)
            {
                _logger.LogError("All {MaxRetries} retry attempts failed with 401 Unauthorized.", MaxRetries);
                throw new HttpRequestException("Authentication failed after retries", null, System.Net.HttpStatusCode.Unauthorized);
            }

            _logger.LogWarning("Received 401 Unauthorized on attempt {Attempt}/{MaxAttempts}. Retrying after {Delay}.",
                attempt + 1, MaxRetries + 1, retryDelay);

            // Force token refresh for next attempt
            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _cachedToken = null; // Invalidate cache to force refresh
            }
            finally
            {
                _tokenLock.Release();
            }

            // Wait before retrying
            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);

            // Exponential backoff
            retryDelay *= 2;
        }

        // This should never be reached, but just in case
        throw new InvalidOperationException("Unexpected end of retry loop");
    }

    /// <summary>
    /// Clones an HttpRequestMessage for retry purposes.
    /// </summary>
    private static HttpRequestMessage CloneHttpRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        // Copy headers (except Authorization which will be set)
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (original.Content != null)
        {
            using (clone.Content = new StreamContent(original.Content.ReadAsStream()))
            {
                foreach (var header in original.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return clone;
    }

    /// <summary>
    /// Callback for proactive token renewal timer.
    /// </summary>
    private async Task ProactiveRenewalCallbackAsync()
    {
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _logger.LogDebug("Starting proactive token renewal");

            await _tokenLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if renewal is still needed
                if (_cachedToken == null || DateTimeOffset.UtcNow >= _tokenExpiry - _renewalBuffer)
                {
                    _logger.LogInformation("Proactively renewing OAuth token before expiry");

                    var tokenResponse = await AcquireTokenAsync(CancellationToken.None).ConfigureAwait(false);

                    _cachedToken = tokenResponse.AccessToken;
                    _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                    _logger.LogInformation("Proactive OAuth token renewal succeeded, new expiry at {Expiry:O}", _tokenExpiry);

                    // Schedule next renewal
                    ScheduleNextRenewal();
                }
                else
                {
                    _logger.LogDebug("Proactive renewal skipped - token still valid");
                    // Schedule next check anyway in case timing was off
                    ScheduleNextRenewal();
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proactive token renewal failed, will retry on next request");

            // Continue with reactive renewal on failures
            // Schedule next attempt (with backoff)
            ScheduleNextRenewalWithBackoff();
        }
    }

    /// <summary>
    /// Schedules the next proactive renewal based on current token expiry.
    /// </summary>
    private void ScheduleNextRenewal()
    {
        if (!_proactiveRenewalEnabled || _renewalTimer == null || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var timeUntilRenewal = _tokenExpiry - _renewalBuffer - DateTimeOffset.UtcNow;

        // Ensure minimum interval (don't schedule too frequently)
        if (timeUntilRenewal < TimeSpan.FromMinutes(1))
        {
            timeUntilRenewal = TimeSpan.FromMinutes(1);
        }

        _renewalTimer.Change(timeUntilRenewal, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Scheduled next proactive renewal in {TimeUntilRenewal}", timeUntilRenewal);
    }

    /// <summary>
    /// Schedules the next renewal attempt with backoff after a failure.
    /// </summary>
    private void ScheduleNextRenewalWithBackoff()
    {
        if (!_proactiveRenewalEnabled || _renewalTimer == null || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Retry after 5 minutes on failure
        var backoffInterval = TimeSpan.FromMinutes(5);
        _renewalTimer.Change(backoffInterval, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Scheduled retry proactive renewal in {BackoffInterval} due to previous failure", backoffInterval);
    }

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// </summary>
    private async Task<string> GetValidTokenAsync(CancellationToken cancellationToken)
    {
        // Fast path: check if cached token is still valid
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry - _renewalBuffer)
        {
            return _cachedToken;
        }

        // Slow path: acquire lock and refresh token
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry - _renewalBuffer)
            {
                return _cachedToken;
            }

            _logger.LogDebug("Refreshing OAuth access token");

            var tokenResponse = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogDebug("OAuth token acquired, expires at {Expiry:O}", _tokenExpiry);

            // Schedule proactive renewal if enabled
            if (_proactiveRenewalEnabled)
            {
                ScheduleNextRenewal();
            }

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Acquires a new access token from the token endpoint.
    /// </summary>
    private async Task<OAuthTokenResponse> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        using var requestContent = new FormUrlEncodedContent(BuildTokenRequestParameters());

        using var response = await _tokenHttpClient.PostAsync(_tokenEndpoint, requestContent, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "OAuth token request failed with status {StatusCode}: {Error}",
                response.StatusCode,
                errorContent);

            throw new HttpRequestException(
                $"Failed to acquire OAuth token. Status: {response.StatusCode}",
                null,
                response.StatusCode);
        }

        var tokenResponse = await response.Content
            .ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("OAuth token response did not contain an access token.");
        }

        return tokenResponse;
    }

    /// <summary>
    /// Builds the form parameters for the token request.
    /// </summary>
    private Dictionary<string, string> BuildTokenRequestParameters()
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        };

        if (!string.IsNullOrWhiteSpace(_scopes))
        {
            parameters["scope"] = _scopes;
        }

        return parameters;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && disposing)
        {
            _renewalTimer?.Dispose();
            _tokenLock.Dispose();
            _tokenHttpClient.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// OAuth token response model.
    /// </summary>
    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

}
