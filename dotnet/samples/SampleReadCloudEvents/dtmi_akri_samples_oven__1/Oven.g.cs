/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace SampleReadCloudEvents.dtmi_akri_samples_oven__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using SampleReadCloudEvents;

    [ModelId("dtmi:akri:samples:oven;1")]
    [TelemetryTopic("akri/samples/{modelId}/{senderId}")]
    public static partial class Oven
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly TelemetryCollectionSender telemetryCollectionSender;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.telemetryCollectionSender = new TelemetryCollectionSender(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public TelemetryCollectionSender TelemetryCollectionSender { get => this.telemetryCollectionSender; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.telemetryCollectionSender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync(bool disposing)
            {
                return ValueTask.CompletedTask;
            }
        }

        public abstract partial class Client
        {
            private readonly TelemetryCollectionReceiver telemetryCollectionReceiver;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.telemetryCollectionReceiver = new TelemetryCollectionReceiver(mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public TelemetryCollectionReceiver TelemetryCollectionReceiver { get => this.telemetryCollectionReceiver; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata);

            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.telemetryCollectionReceiver.StartAsync(cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.telemetryCollectionReceiver.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
        }
    }
}
