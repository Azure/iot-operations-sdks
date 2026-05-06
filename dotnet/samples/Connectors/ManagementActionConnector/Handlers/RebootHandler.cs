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
    /// Handles the <c>device-control::reboot</c> Call action. Parses a
    /// <see cref="RebootRequest"/>, asks the <see cref="FakeDevice"/> to begin a
    /// (simulated) reboot, and returns a <see cref="RebootResponse"/> with the
    /// scheduled time and a request id.
    /// </summary>
    /// <remarks>
    /// Demonstrates: typed JSON request/response, <see cref="ManagementActionApplicationError"/>
    /// for the "already rebooting" case (mapped from <see cref="DeviceBusyException"/>),
    /// async device work that honors <see cref="CancellationToken"/>.
    /// </remarks>
    public sealed class RebootHandler : IManagementActionHandler
    {
        private static readonly TimeSpan RebootDuration = TimeSpan.FromSeconds(2);

        private readonly ILogger _logger;
        private readonly FakeDevice _device;
        private readonly string _deviceName;
        private readonly string _assetName;

        public RebootHandler(ILogger logger, FakeDevice device, string deviceName, string assetName)
        {
            _logger = logger;
            _device = device;
            _deviceName = deviceName;
            _assetName = assetName;
        }

        public async Task<ManagementActionResponse> HandleCallAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Reboot invoked on {Device}/{Asset} ({Bytes} bytes, content-type={ContentType})",
                _deviceName, _assetName, args.Payload.Length, args.ContentType);

            RebootRequest request;
            try
            {
                request = args.Payload.Length == 0
                    ? new RebootRequest()
                    : JsonSerializer.Deserialize<RebootRequest>(args.Payload.ToArray()) ?? new RebootRequest();
            }
            catch (JsonException ex)
            {
                return ResponseHelpers.ApplicationError("InvalidPayload", $"Failed to parse RebootRequest: {ex.Message}");
            }

            try
            {
                Guid requestId = await _device.BeginRebootAsync(request.Force, RebootDuration, cancellationToken);
                var response = new RebootResponse
                {
                    RequestId = requestId,
                    ScheduledAtUtc = DateTime.UtcNow,
                    RebootCount = _device.RebootCount,
                };
                return ResponseHelpers.Json(response);
            }
            catch (DeviceBusyException ex)
            {
                return ResponseHelpers.ApplicationError("AlreadyRebooting", ex.Message);
            }
        }

        public Task<ManagementActionResponse> HandleReadAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
            => throw new InvalidOperationException("RebootHandler is wired to a Call action; HandleReadAsync should never be called.");

        public Task<ManagementActionResponse> HandleWriteAsync(ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
            => throw new InvalidOperationException("RebootHandler is wired to a Call action; HandleWriteAsync should never be called.");

        public ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing RebootHandler for {Device}/{Asset}", _deviceName, _assetName);
            return ValueTask.CompletedTask;
        }
    }
}
