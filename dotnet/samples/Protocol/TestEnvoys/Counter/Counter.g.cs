/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

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
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
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

            /// <summary>
            /// Construct a new instance of this service.
            /// </summary>
            /// <param name="applicationContext">The shared context for your application.</param>
            /// <param name="mqttClient">The MQTT client to use.</param>
            /// <param name="topicTokenMap">
            /// The topic token replacement map to use for all operations by default. Generally, this will include the token values
            /// for topic tokens such as "modelId" which should be the same for the duration of this service's lifetime. Note that
            /// additional topic tokens can be specified when starting the service with <see cref="StartAsync(Dictionary{string, string}?, int?, CancellationToken)"/> and
            /// can be specified per-telemetry message.
            /// </param>
            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                this.readCounterCommandExecutor = new ReadCounterCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ReadCounterInt };
                this.incrementCommandExecutor = new IncrementCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = IncrementInt };
                this.resetCommandExecutor = new ResetCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ResetInt };
                this.telemetrySender = new TelemetrySender(applicationContext, mqttClient);

                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.readCounterCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                        this.incrementCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                        this.resetCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                        this.telemetrySender.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }

                this.readCounterCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
                this.incrementCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
                this.resetCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
            }

            public ReadCounterCommandExecutor ReadCounterCommandExecutor { get => this.readCounterCommandExecutor; }

            public IncrementCommandExecutor IncrementCommandExecutor { get => this.incrementCommandExecutor; }

            public ResetCommandExecutor ResetCommandExecutor { get => this.resetCommandExecutor; }

            public TelemetrySender TelemetrySender { get => this.telemetrySender; }

            public abstract Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            /// <summary>
            /// Send telemetry.
            /// </summary>
            /// <param name="telemetry">The payload of the telemetry.</param>
            /// <param name="metadata">The metadata of the telemetry.</param>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacement map to use in addition to the topic token map provided in the constructor. If this map
            /// contains any keys that topic token map provided in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="qos">The quality of service to send the telemetry with.</param>
            /// <param name="telemetryTimeout">How long the telemetry message will be available on the broker for a receiver to receive.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }
                await this.telemetrySender.SendTelemetryAsync(telemetry, metadata, prefixedAdditionalTopicTokenMap, qos, telemetryTimeout, cancellationToken);
            }

            /// <summary>
            /// Begin accepting command invocations for all command executors.
            /// </summary>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacements to use in addition to any topic tokens specified in the constructor. If this map
            /// contains any keys that topic tokens provided in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="preferredDispatchConcurrency">The dispatch concurrency count for the command response cache to use.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <remarks>
            /// Specifying custom topic tokens in <paramref name="additionalTopicTokenMap"/> allows you to make command executors only
            /// accept commands over a specific topic.
            ///
            /// Note that a given command executor can only be started with one set of topic token replacements. If you want a command executor
            /// to only handle commands for several specific sets of topic token values (as opposed to all possible topic token values), then you will
            /// instead need to create a command executor per topic token set.
            /// </remarks>
            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                await Task.WhenAll(
                    this.readCounterCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.incrementCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.resetCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
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

            /// <summary>
            /// Construct a new instance of this client.
            /// </summary>
            /// <param name="applicationContext">The shared context for your application.</param>
            /// <param name="mqttClient">The MQTT client to use.</param>
            /// <param name="topicTokenMap">
            /// The topic token replacement map to use for all operations by default. Generally, this will include the token values
            /// for topic tokens such as "modelId" which should be the same for the duration of this client's lifetime. Note that
            /// additional topic tokens can be specified when starting the client with <see cref="StartAsync(Dictionary{string, string}?, int?, CancellationToken)"/>.
            /// </param>
            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.readCounterCommandInvoker = new ReadCounterCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.readCounterCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.incrementCommandInvoker = new IncrementCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.incrementCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.resetCommandInvoker = new ResetCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.resetCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.telemetryReceiver = new TelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.telemetryReceiver.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public ReadCounterCommandInvoker ReadCounterCommandInvoker { get => this.readCounterCommandInvoker; }

            public IncrementCommandInvoker IncrementCommandInvoker { get => this.incrementCommandInvoker; }

            public ResetCommandInvoker ResetCommandInvoker { get => this.resetCommandInvoker; }

            public TelemetryReceiver TelemetryReceiver { get => this.telemetryReceiver; }

            public abstract Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata);

            /// <summary>
            /// Invoke a command.
            /// </summary>
            /// <param name="requestMetadata">The metadata for this command request.</param>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacement map to use in addition to the topic tokens specified in the constructor. If this map
            /// contains any keys that the topic tokens specified in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="commandTimeout">How long the command will be available on the broker for an executor to receive.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The command response.</returns>
            public RpcCallAsync<ReadCounterResponsePayload> ReadCounterAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }

                prefixedAdditionalTopicTokenMap["invokerClientId"] = clientId;
                prefixedAdditionalTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<ReadCounterResponsePayload>(this.readCounterCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            /// <summary>
            /// Invoke a command.
            /// </summary>
            /// <param name="requestMetadata">The metadata for this command request.</param>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacement map to use in addition to the topic tokens specified in the constructor. If this map
            /// contains any keys that the topic tokens specified in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="commandTimeout">How long the command will be available on the broker for an executor to receive.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The command response.</returns>
            public RpcCallAsync<IncrementResponsePayload> IncrementAsync(string executorId, IncrementRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }

                prefixedAdditionalTopicTokenMap["invokerClientId"] = clientId;
                prefixedAdditionalTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<IncrementResponsePayload>(this.incrementCommandInvoker.InvokeCommandAsync(request, metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            /// <summary>
            /// Invoke a command.
            /// </summary>
            /// <param name="requestMetadata">The metadata for this command request.</param>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacement map to use in addition to the topic tokens specified in the constructor. If this map
            /// contains any keys that the topic tokens specified in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="commandTimeout">How long the command will be available on the broker for an executor to receive.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The command response.</returns>
            public RpcCallAsync<EmptyJson> ResetAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }

                prefixedAdditionalTopicTokenMap["invokerClientId"] = clientId;
                prefixedAdditionalTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<EmptyJson>(this.resetCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            /// <summary>
            /// Begin accepting telemetry for all telemetry receivers.
            /// </summary>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacements to use in addition to any topic tokens specified in the constructor. If this map
            /// contains any keys that topic tokens provided in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <remarks>
            /// Specifying custom topic tokens in <paramref name="additionalTopicTokenMap"/> allows you to make telemetry receivers only
            /// accept telemetry over a specific topic.
            ///
            /// Note that a given telemetry receiver can only be started with one set of topic token replacements. If you want a telemetry receiver
            /// to only handle telemetry for several specific sets of topic token values (as opposed to all possible topic token values), then you will
            /// instead need to create a telemetry receiver per topic token set.
            /// </remarks>
            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.telemetryReceiver.StartAsync(cancellationToken)).ConfigureAwait(false);
            }

            /// <summary>
            /// Stop accepting telemetry for all telemetry receivers.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token.</param>
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
