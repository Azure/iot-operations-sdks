/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Counter
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

    [CommandTopic("rpc/command-samples/{executorId}/{commandName}")]
    [TelemetryTopic("telemetry/telemetry-samples/counterValue")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class Counter
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly ReadCounterCommandExecutor readCounterCommandExecutor;
            private readonly IncrementCommandExecutor incrementCommandExecutor;
            private readonly ResetCommandExecutor resetCommandExecutor;
            private readonly TelemetrySender telemetrySender;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.readCounterCommandExecutor = new ReadCounterCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ReadCounterInt};
                this.readCounterCommandExecutor.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.incrementCommandExecutor = new IncrementCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = IncrementInt};
                this.incrementCommandExecutor.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.resetCommandExecutor = new ResetCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ResetInt};
                this.resetCommandExecutor.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.telemetrySender = new TelemetrySender(applicationContext, mqttClient);
                this.telemetrySender.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            public ReadCounterCommandExecutor ReadCounterCommandExecutor { get => this.readCounterCommandExecutor; }
            public IncrementCommandExecutor IncrementCommandExecutor { get => this.incrementCommandExecutor; }
            public ResetCommandExecutor ResetCommandExecutor { get => this.resetCommandExecutor; }
            public TelemetrySender TelemetrySender { get => this.telemetrySender; }


            public abstract Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.telemetrySender.SendTelemetryAsync(telemetry, metadata, transientTopicTokenMap?.Select(kvp => new KeyValuePair<string, string>($"ex:{kvp.Key}", kvp.Value))?.ToDictionary(), qos, messageExpiryInterval, cancellationToken);
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
                    this.readCounterCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken),
                    this.incrementCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken),
                    this.resetCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.readCounterCommandExecutor.StopAsync(cancellationToken),
                    this.incrementCommandExecutor.StopAsync(cancellationToken),
                    this.resetCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterInt(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<ReadCounterResponsePayload> extended = await this.ReadCounterAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<ReadCounterResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<IncrementResponsePayload>> IncrementInt(ExtendedRequest<IncrementRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<IncrementResponsePayload> extended = await this.IncrementAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<IncrementResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<EmptyJson>> ResetInt(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.ResetAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyJson> { ResponseMetadata = responseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.readCounterCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.incrementCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.resetCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.telemetrySender.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCounterCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.incrementCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.resetCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.telemetrySender.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly ReadCounterCommandInvoker readCounterCommandInvoker;
            private readonly IncrementCommandInvoker incrementCommandInvoker;
            private readonly ResetCommandInvoker resetCommandInvoker;
            private readonly TelemetryReceiver telemetryReceiver;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.readCounterCommandInvoker = new ReadCounterCommandInvoker(applicationContext, mqttClient);
                this.readCounterCommandInvoker.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.incrementCommandInvoker = new IncrementCommandInvoker(applicationContext, mqttClient);
                this.incrementCommandInvoker.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.resetCommandInvoker = new ResetCommandInvoker(applicationContext, mqttClient);
                this.resetCommandInvoker.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                this.telemetryReceiver = new TelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                this.telemetryReceiver.TopicTokenReplacementMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            public ReadCounterCommandInvoker ReadCounterCommandInvoker { get => this.readCounterCommandInvoker; }
            public IncrementCommandInvoker IncrementCommandInvoker { get => this.incrementCommandInvoker; }
            public ResetCommandInvoker ResetCommandInvoker { get => this.resetCommandInvoker; }
            public TelemetryReceiver TelemetryReceiver { get => this.telemetryReceiver; }


            public abstract Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata);

            public RpcCallAsync<ReadCounterResponsePayload> ReadCounterAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();
                var combinedTopicTokenMap = TopicTokenReplacementMap.Concat(topicTokenMap).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                combinedTopicTokenMap["invokerClientId"] = clientId;
                combinedTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<ReadCounterResponsePayload>(this.readCounterCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, combinedTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<IncrementResponsePayload> IncrementAsync(string executorId, IncrementRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();
                var combinedTopicTokenMap = TopicTokenReplacementMap.Concat(topicTokenMap).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                combinedTopicTokenMap["invokerClientId"] = clientId;
                combinedTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<IncrementResponsePayload>(this.incrementCommandInvoker.InvokeCommandAsync(request, metadata, combinedTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<EmptyJson> ResetAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();
                var combinedTopicTokenMap = TopicTokenReplacementMap.Concat(topicTokenMap).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                combinedTopicTokenMap["invokerClientId"] = clientId;
                combinedTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<EmptyJson>(this.resetCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, combinedTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
                await this.readCounterCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.incrementCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.resetCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.telemetryReceiver.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCounterCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.incrementCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.resetCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.telemetryReceiver.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
