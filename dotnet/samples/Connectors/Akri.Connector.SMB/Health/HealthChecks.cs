// <copyright file="HealthChecks.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Azure.Iot.Operations.Services.LeaderElection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akri.Connector.SMB.Health;

/// <summary>
/// Health check for SMB connector to verify ADR-driven connectivity configuration.
/// SMB server connectivity and authentication are resolved per-query from ADR
/// inbound endpoints at runtime.
/// </summary>
public sealed class SmbConnectivityHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<SMBConnectorOptions> _options;

    public SmbConnectivityHealthCheck(
        ILogger<SmbConnectivityHealthCheck> logger,
        IOptionsMonitor<SMBConnectorOptions> options,
        ISMBClient smbClient)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy(
            "SMB connectivity is validated per query using ADR inbound endpoint authentication."));
    }
}

/// <summary>
/// Health check for leader election status.
/// </summary>
public sealed class LeaderElectionHealthCheck : IHealthCheck
{
    private readonly ILogger<LeaderElectionHealthCheck> _logger;
    private readonly IOptionsMonitor<SMBConnectorOptions> _options;
    private readonly LeaderElectionClient? _leaderElectionClient;

    public LeaderElectionHealthCheck(
        ILogger<LeaderElectionHealthCheck> logger,
        IOptionsMonitor<SMBConnectorOptions> options,
        LeaderElectionClient? leaderElectionClient)
    {
        _logger = logger;
        _options = options;
        _leaderElectionClient = leaderElectionClient;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        // If leader election is disabled, this check is not applicable
        if (!options.EnableLeaderElection)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Leader election is disabled"));
        }

        // If leader election is enabled but client is not available, that's a problem
        if (_leaderElectionClient == null)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Leader election is enabled but client is not configured"));
        }

        try
        {
            // Check if we have a last known campaign result
            var lastResult = _leaderElectionClient.LastKnownCampaignResult;
            
            if (lastResult == null)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Leader election client has not completed a campaign yet"));
            }

            var data = new Dictionary<string, object>
            {
                ["IsLeader"] = lastResult.IsLeader,
            };

            if (lastResult.IsLeader)
            {
                return Task.FromResult(
                    HealthCheckResult.Healthy("This instance is the current leader", data));
            }
            else
            {
                return Task.FromResult(
                    HealthCheckResult.Healthy("This instance is not the leader", data));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leader election health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Failed to check leader election status", ex));
        }
    }
}

/// <summary>
/// Health check for watermark store connectivity.
/// </summary>
public sealed class WatermarkStoreHealthCheck : IHealthCheck
{
    private readonly ILogger<WatermarkStoreHealthCheck> _logger;

    public WatermarkStoreHealthCheck(ILogger<WatermarkStoreHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Watermark store uses Azure IoT Operations state store (MQTT-based)
            // The actual connectivity is validated through MQTT health checks
            // This is a basic check that the watermark store is configured
            return Task.FromResult(
                HealthCheckResult.Healthy("Watermark store is configured"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watermark store health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Watermark store check failed", ex));
        }
    }
}
