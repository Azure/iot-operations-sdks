/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.CustomTopicTokens
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
    using TestEnvoys;

    [CommandTopic("rpc/command-samples/{executorId}/{commandName}/{ex:myCustomTopicToken}")]
    [TelemetryTopic("telemetry/telemetry-samples/{ex:myCustomTopicToken}")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class CustomTopicTokens
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly ReadCustomTopicTokenCommandExecutor readCustomTopicTokenCommandExecutor;
            private readonly TelemetrySender telemetrySender;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.readCustomTopicTokenCommandExecutor = new ReadCustomTopicTokenCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ReadCustomTopicTokenInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.readCustomTopicTokenCommandInvoker.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
                this.telemetrySender = new TelemetrySender(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.telemetrySender.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
            }

            public ReadCustomTopicTokenCommandExecutor ReadCustomTopicTokenCommandExecutor { get => this.readCustomTopicTokenCommandExecutor; }
            public TelemetrySender TelemetrySender { get => this.telemetrySender; }


            public abstract Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? transientTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.telemetrySender.SendTelemetryAsync(telemetry, metadata, transientTopicTokenMap, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task StartAsync(Dictionary<string, string>? topicTokenMap = null, int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                topicTokenMap ??= new();
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                topicTokenMap["executorId"] = clientId;

                await Task.WhenAll(
                    this.readCustomTopicTokenCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.readCustomTopicTokenCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenInt(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<ReadCustomTopicTokenResponsePayload> extended = await this.ReadCustomTopicTokenAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<ReadCustomTopicTokenResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.readCustomTopicTokenCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.telemetrySender.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCustomTopicTokenCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.telemetrySender.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly ReadCustomTopicTokenCommandInvoker readCustomTopicTokenCommandInvoker;
            private readonly TelemetryReceiver telemetryReceiver;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.readCustomTopicTokenCommandInvoker = new ReadCustomTopicTokenCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.readCustomTopicTokenCommandInvoker.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
                this.telemetryReceiver = new TelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.telemetryReceiver.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
            }

            public ReadCustomTopicTokenCommandInvoker ReadCustomTopicTokenCommandInvoker { get => this.readCustomTopicTokenCommandInvoker; }
            public TelemetryReceiver TelemetryReceiver { get => this.telemetryReceiver; }


            public abstract Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata);

            public RpcCallAsync<ReadCustomTopicTokenResponsePayload> ReadCustomTopicTokenAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();

                topicTokenMap["invokerClientId"] = clientId;
                topicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<ReadCustomTopicTokenResponsePayload>(this.readCustomTopicTokenCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, topicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async Task StartAsync(Dictionary<string, string>? topicTokenMap = null, CancellationToken cancellationToken = default)
            {
                topicTokenMap ??= new();

                await Task.WhenAll(
                    this.telemetryReceiver.StartAsync(topicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.telemetryReceiver.StopAsync(cancellationToken)).ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
            {
                await this.readCustomTopicTokenCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.telemetryReceiver.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCustomTopicTokenCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.telemetryReceiver.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
