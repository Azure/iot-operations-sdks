/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace JsonComm.dtmi_codegen_communicationTest_jsonModel__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using JsonComm;

    [TelemetryTopic("test/JsonModel/{senderId}/telemetry")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public static partial class JsonModel
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private IMqttPubSubClient mqttClient;
            private readonly TelemetryCollectionSender telemetryCollectionSender;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.telemetryCollectionSender = new TelemetryCollectionSender(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public TelemetryCollectionSender TelemetryCollectionSender { get => this.telemetryCollectionSender; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.telemetryCollectionSender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                await this.telemetryCollectionSender.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.telemetryCollectionSender.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client
        {
            private IMqttPubSubClient mqttClient;
            private readonly TelemetryCollectionReceiver telemetryCollectionReceiver;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
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

            public async ValueTask DisposeAsync()
            {
                await this.telemetryCollectionReceiver.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.telemetryCollectionReceiver.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
