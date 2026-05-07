// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService;
using static Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // An interface for the code gen'd AdrBaseServiceServiceStub so that we can mock it in our unit tests
    internal interface IAdrBaseServiceServiceStub : IAsyncDisposable
    {
        DeviceEndpointRuntimeHealthEventTelemetrySender DeviceEndpointRuntimeHealthEventTelemetrySender { get; }

        DatasetRuntimeHealthEventTelemetrySender DatasetRuntimeHealthEventTelemetrySender { get; }

        EventRuntimeHealthEventTelemetrySender EventRuntimeHealthEventTelemetrySender { get; }

        ManagementActionRuntimeHealthEventTelemetrySender ManagementActionRuntimeHealthEventTelemetrySender { get; }

        StreamRuntimeHealthEventTelemetrySender StreamRuntimeHealthEventTelemetrySender { get; }

        Task StopAsync(CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(DeviceEndpointRuntimeHealthEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(DatasetRuntimeHealthEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)

        Task SendTelemetryAsync(EventRuntimeHealthEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(ManagementActionRuntimeHealthEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(StreamRuntimeHealthEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(DeviceUpdateEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        Task SendTelemetryAsync(AssetUpdateEventTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);

        ValueTask DisposeAsync(bool disposing, CancellationToken cancellationToken);

        ValueTask DisposeAsync(bool disposing);

        ValueTask DisposeAsync(CancellationToken cancellationToken);
    }
}
