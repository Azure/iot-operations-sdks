// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using ManagementActionConnector.Contracts;
using ManagementActionConnector.Devices;
using Microsoft.Extensions.Logging;

namespace ManagementActionConnector.Handlers
{
    /// <summary>
    /// Handles the <c>device-control::read-temperature</c> Read action. Returns
    /// a <see cref="TemperatureReading"/> sampled from the shared <see cref="FakeDevice"/>.
    /// </summary>
    /// <remarks>
    /// Demonstrates: empty request payload, structured response, cross-action state
    /// (the <c>unit</c> reflects the most recent <c>write-configuration</c>), and
    /// <see cref="ManagementActionApplicationError"/> for the "device unavailable"
    /// case (mapped from <see cref="DeviceUnavailableException"/>).
    /// </remarks>
    public sealed class ReadTemperatureHandler : IManagementActionHandler
    {
        private readonly ILogger _logger;
        private readonly FakeDevice _device;
        private readonly string _deviceName;
        private readonly string _assetName;

        public ReadTemperatureHandler(ILogger logger, FakeDevice device, string deviceName, string assetName)
        {
            _logger = logger;
            _device = device;
            _deviceName = deviceName;
            _assetName = assetName;
        }

        public Task<ManagementActionResponse> HandleCallAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
            => throw new InvalidOperationException("ReadTemperatureHandler is wired to a Read action; HandleCallAsync should never be called.");

        public async Task<ManagementActionResponse> HandleReadAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ReadTemperature invoked on {Device}/{Asset}", _deviceName, _assetName);

            try
            {
                double value = await _device.ReadTemperatureAsync(cancellationToken);
                var reading = new TemperatureReading
                {
                    Value = Math.Round(value, 2),
                    Unit = _device.Configuration.Unit,
                    SampledAtUtc = DateTime.UtcNow,
                };
                return ResponseHelpers.Json(reading);
            }
            catch (DeviceUnavailableException ex)
            {
                return ResponseHelpers.ApplicationError("DeviceUnavailable", ex.Message);
            }
        }

        public Task<ManagementActionResponse> HandleWriteAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
            => throw new InvalidOperationException("ReadTemperatureHandler is wired to a Read action; HandleWriteAsync should never be called.");

        public ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing ReadTemperatureHandler for {Device}/{Asset}", _deviceName, _assetName);
            return ValueTask.CompletedTask;
        }
    }
}
