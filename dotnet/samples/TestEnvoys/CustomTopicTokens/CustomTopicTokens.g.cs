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

                this.readCustomTopicTokenCommandExecutor = new ReadCustomTopicTokenCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ReadCustomTopicTokenInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        if (!topicTokenKey.StartsWith("ex:"))
                        {
                            throw new ArgumentException("All custom topic token keys must be prefixed with \"ex:\". Provided key: " + topicTokenKey);
                        }
                        this.readCustomTopicTokenCommandExecutor.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.telemetrySender = new TelemetrySender(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        if (!topicTokenKey.StartsWith("ex:"))
                        {
                            throw new ArgumentException("All custom topic token keys must be prefixed with \"ex:\". Provided key: " + topicTokenKey);
                        }
                        this.telemetrySender.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public ReadCustomTopicTokenCommandExecutor ReadCustomTopicTokenCommandExecutor { get => this.readCustomTopicTokenCommandExecutor; }
            public TelemetrySender TelemetrySender { get => this.telemetrySender; }


            public abstract Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
            public async Task StartAsync(Dictionary<string, string>? additionalTopicTokenMap = null, int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                additionalTopicTokenMap["executorId"] = clientId;

                await Task.WhenAll(
                    this.readCustomTopicTokenCommandExecutor.StartAsync(additionalTopicTokenMap, preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
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

                this.readCustomTopicTokenCommandInvoker = new ReadCustomTopicTokenCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        if (!topicTokenKey.StartsWith("ex:"))
                        {
                            throw new ArgumentException("All custom topic token keys must be prefixed with \"ex:\". Provided key: " + topicTokenKey);
                        }

                        this.readCustomTopicTokenCommandInvoker.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.telemetryReceiver = new TelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        if (!topicTokenKey.StartsWith("ex:"))
                        {
                            throw new ArgumentException("All custom topic token keys must be prefixed with \"ex:\". Provided key: " + topicTokenKey);
                        }

                        this.telemetryReceiver.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public ReadCustomTopicTokenCommandInvoker ReadCustomTopicTokenCommandInvoker { get => this.readCustomTopicTokenCommandInvoker; }
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
            public RpcCallAsync<ReadCustomTopicTokenResponsePayload> ReadCustomTopicTokenAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                additionalTopicTokenMap["invokerClientId"] = clientId;
                additionalTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<ReadCustomTopicTokenResponsePayload>(this.readCustomTopicTokenCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, additionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
            public async Task StartAsync(Dictionary<string, string>? additionalTopicTokenMap = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();

                await Task.WhenAll(
                    this.telemetryReceiver.StartAsync(additionalTopicTokenMap, cancellationToken)).ConfigureAwait(false);
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
