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
    /// Handles the <c>device-control::identify</c> Call action. Parses an
    /// <see cref="IdentifyRequest"/>, asks the <see cref="FakeDevice"/> to blink its
    /// locator indicator, and returns an <see cref="IdentifyResponse"/> with a request
    /// id and the running identify count.
    /// </summary>
    /// <remarks>
    /// Demonstrates: typed JSON request/response, <see cref="ManagementActionApplicationError"/>
    /// for an out-of-range <c>blinkCount</c>, async device work that honors
    /// <see cref="CancellationToken"/>. Unlike a reboot, identify is non-disruptive: it never
    /// makes the device unavailable, so it does not interfere with concurrent read/write actions.
    /// </remarks>
    public sealed class IdentifyHandler : IManagementActionHandler
    {
        private const int MinBlinkCount = 1;
        private const int MaxBlinkCount = 10;

        private readonly ILogger _logger;
        private readonly FakeDevice _device;

        public IdentifyHandler(ILogger logger, FakeDevice device)
        {
            _logger = logger;
            _device = device;
        }

        public async Task<ManagementActionResponse> HandleAsync(
            ManagementActionInvokedEventArgs args, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Identify invoked on {Device}/{Asset} ({Bytes} bytes, content-type={ContentType})",
                args.DeviceName, args.AssetName, args.Payload.Length, args.ContentType);

            IdentifyRequest request;
            try
            {
                request = args.Payload.Length == 0
                    ? new IdentifyRequest()
                    : JsonSerializer.Deserialize<IdentifyRequest>(args.Payload.ToArray()) ?? new IdentifyRequest();
            }
            catch (JsonException ex)
            {
                return ResponseHelpers.ApplicationError("InvalidPayload", $"Failed to parse IdentifyRequest: {ex.Message}");
            }

            if (request.BlinkCount < MinBlinkCount || request.BlinkCount > MaxBlinkCount)
            {
                return ResponseHelpers.ApplicationError(
                    "ValidationFailed",
                    $"blinkCount must be between {MinBlinkCount} and {MaxBlinkCount}; got {request.BlinkCount}.");
            }

            Guid requestId = await _device.IdentifyAsync(cancellationToken);
            var response = new IdentifyResponse
            {
                RequestId = requestId,
                BlinkCount = request.BlinkCount,
                IdentifyCount = _device.IdentifyCount,
            };
            return ResponseHelpers.Json(response);
        }


        public ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing IdentifyHandler");
            return ValueTask.CompletedTask;
        }
    }
}
