// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Connector;
using ManagementActionConnector.Contracts;
using ManagementActionConnector.Devices;

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
        private readonly IManagementActionStatusReporter _statusReporter;

        public RebootHandler(ILogger logger, FakeDevice device, IManagementActionStatusReporter statusReporter)
        {
            _logger = logger;
            _device = device;
            _statusReporter = statusReporter;
        }

        public async Task<ManagementActionResponse> HandleAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Reboot invoked on {Device}/{Asset} ({Bytes} bytes, content-type={ContentType})",
                args.DeviceName, args.AssetName, args.Payload.Length, args.ContentType);

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
                // Device responded successfully — make sure any prior Unavailable state is cleared.
                await _statusReporter.ReportAvailableAsync(cancellationToken);
                return ResponseHelpers.Json(response);
            }
            catch (DeviceBusyException ex)
            {
                return ResponseHelpers.ApplicationError("AlreadyRebooting", ex.Message);
            }
            catch (DeviceUnavailableException ex)
            {
                // Runtime drift: device was reachable at config time, isn't now.
                // Flip the action's runtime health so operators see why invocations are failing.
                await _statusReporter.ReportUnavailableAsync(ex.Message, cancellationToken);
                return ResponseHelpers.ApplicationError("DeviceUnavailable", ex.Message);
            }
        }


        public ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing RebootHandler");
            return ValueTask.CompletedTask;
        }
    }
}
