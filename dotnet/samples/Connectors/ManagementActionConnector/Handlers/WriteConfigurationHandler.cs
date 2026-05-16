// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Connector;
using ManagementActionConnector.Contracts;
using ManagementActionConnector.Devices;
using Microsoft.Extensions.Logging;

namespace ManagementActionConnector.Handlers
{
    /// <summary>
    /// Handles the <c>device-control::write-configuration</c> Write action. Validates
    /// a <see cref="ConfigurationUpdate"/>, applies it to the shared <see cref="FakeDevice"/>,
    /// and returns a <see cref="ConfigurationAck"/>.
    /// </summary>
    /// <remarks>
    /// Demonstrates: payload-in / minimal-out semantics, validation that produces
    /// a <see cref="ManagementActionApplicationError"/>, and an observable side
    /// effect (subsequent <c>read-temperature</c> reflects the new unit).
    /// </remarks>
    public sealed class WriteConfigurationHandler : IManagementActionHandler
    {
        private const int MinSampleIntervalMs = 100;
        private const int MaxSampleIntervalMs = 60_000;

        private readonly ILogger _logger;
        private readonly FakeDevice _device;

        public WriteConfigurationHandler(ILogger logger, FakeDevice device)
        {
            _logger = logger;
            _device = device;
        }

        public async Task<ManagementActionResponse> HandleAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "WriteConfiguration invoked on {Device}/{Asset} ({Bytes} bytes)",
                args.DeviceName, args.AssetName, args.Payload.Length);

            ConfigurationUpdate? update;
            try
            {
                update = JsonSerializer.Deserialize<ConfigurationUpdate>(args.Payload.ToArray());
            }
            catch (JsonException ex)
            {
                return ResponseHelpers.ApplicationError("InvalidPayload", $"Failed to parse ConfigurationUpdate: {ex.Message}");
            }

            if (update is null)
            {
                return ResponseHelpers.ApplicationError("InvalidPayload", "Empty ConfigurationUpdate payload.");
            }

            if (update.SampleIntervalMs < MinSampleIntervalMs || update.SampleIntervalMs > MaxSampleIntervalMs)
            {
                return ResponseHelpers.ApplicationError(
                    "ValidationFailed",
                    $"sampleIntervalMs must be between {MinSampleIntervalMs} and {MaxSampleIntervalMs}; got {update.SampleIntervalMs}.");
            }

            if (!IsValidUnit(update.Unit))
            {
                return ResponseHelpers.ApplicationError(
                    "ValidationFailed",
                    $"unit must be 'C' or 'F'; got '{update.Unit}'.");
            }

            var applied = await _device.WriteConfigurationAsync(
                new DeviceConfig(update.SampleIntervalMs, update.Unit),
                cancellationToken);

            var ack = new ConfigurationAck
            {
                AppliedAtUtc = DateTime.UtcNow,
                AppliedSampleIntervalMs = applied.SampleIntervalMs,
                AppliedUnit = applied.Unit,
            };
            return ResponseHelpers.Json(ack);
        }

        private static bool IsValidUnit(string unit)
            => string.Equals(unit, "C", StringComparison.OrdinalIgnoreCase)
                || string.Equals(unit, "F", StringComparison.OrdinalIgnoreCase);

        public ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing WriteConfigurationHandler");
            return ValueTask.CompletedTask;
        }
    }
}
