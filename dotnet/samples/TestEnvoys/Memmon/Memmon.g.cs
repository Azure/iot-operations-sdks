/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Memmon
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

    [CommandTopic("rpc/samples/{modelId}/{executorId}/{commandName}")]
    [TelemetryTopic("rpc/samples/{modelId}/{senderId}/{telemetryName}")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class Memmon
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly StartTelemetryCommandExecutor startTelemetryCommandExecutor;
            private readonly StopTelemetryCommandExecutor stopTelemetryCommandExecutor;
            private readonly GetRuntimeStatsCommandExecutor getRuntimeStatsCommandExecutor;
            private readonly WorkingSetTelemetrySender workingSetTelemetrySender;
            private readonly ManagedMemoryTelemetrySender managedMemoryTelemetrySender;
            private readonly MemoryStatsTelemetrySender memoryStatsTelemetrySender;

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

                this.startTelemetryCommandExecutor = new StartTelemetryCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = StartTelemetryInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.startTelemetryCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }

                this.startTelemetryCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
                this.stopTelemetryCommandExecutor = new StopTelemetryCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = StopTelemetryInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.stopTelemetryCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }

                this.stopTelemetryCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
                this.getRuntimeStatsCommandExecutor = new GetRuntimeStatsCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = GetRuntimeStatsInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.getRuntimeStatsCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }

                this.getRuntimeStatsCommandExecutor.TopicTokenMap.TryAdd("executorId", clientId);
                this.workingSetTelemetrySender = new WorkingSetTelemetrySender(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.workingSetTelemetrySender.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.managedMemoryTelemetrySender = new ManagedMemoryTelemetrySender(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.managedMemoryTelemetrySender.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.memoryStatsTelemetrySender = new MemoryStatsTelemetrySender(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.memoryStatsTelemetrySender.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public StartTelemetryCommandExecutor StartTelemetryCommandExecutor { get => this.startTelemetryCommandExecutor; }
            public StopTelemetryCommandExecutor StopTelemetryCommandExecutor { get => this.stopTelemetryCommandExecutor; }
            public GetRuntimeStatsCommandExecutor GetRuntimeStatsCommandExecutor { get => this.getRuntimeStatsCommandExecutor; }
            public WorkingSetTelemetrySender WorkingSetTelemetrySender { get => this.workingSetTelemetrySender; }
            public ManagedMemoryTelemetrySender ManagedMemoryTelemetrySender { get => this.managedMemoryTelemetrySender; }
            public MemoryStatsTelemetrySender MemoryStatsTelemetrySender { get => this.memoryStatsTelemetrySender; }


            public abstract Task<CommandResponseMetadata?> StartTelemetryAsync(StartTelemetryRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsAsync(GetRuntimeStatsRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
            public async Task SendTelemetryAsync(WorkingSetTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }
                await this.workingSetTelemetrySender.SendTelemetryAsync(telemetry, metadata, prefixedAdditionalTopicTokenMap, qos, telemetryTimeout, cancellationToken);
            }

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
            public async Task SendTelemetryAsync(ManagedMemoryTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }
                await this.managedMemoryTelemetrySender.SendTelemetryAsync(telemetry, metadata, prefixedAdditionalTopicTokenMap, qos, telemetryTimeout, cancellationToken);
            }

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
            public async Task SendTelemetryAsync(MemoryStatsTelemetry telemetry, OutgoingTelemetryMetadata metadata, Dictionary<string, string>? additionalTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }
                await this.memoryStatsTelemetrySender.SendTelemetryAsync(telemetry, metadata, prefixedAdditionalTopicTokenMap, qos, telemetryTimeout, cancellationToken);
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
                    this.startTelemetryCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.stopTelemetryCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.getRuntimeStatsCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.startTelemetryCommandExecutor.StopAsync(cancellationToken),
                    this.stopTelemetryCommandExecutor.StopAsync(cancellationToken),
                    this.getRuntimeStatsCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<EmptyAvro>> StartTelemetryInt(ExtendedRequest<StartTelemetryRequestPayload> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StartTelemetryAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<EmptyAvro>> StopTelemetryInt(ExtendedRequest<EmptyAvro> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StopTelemetryAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsInt(ExtendedRequest<GetRuntimeStatsRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetRuntimeStatsResponsePayload> extended = await this.GetRuntimeStatsAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetRuntimeStatsResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.startTelemetryCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.stopTelemetryCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.getRuntimeStatsCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.workingSetTelemetrySender.DisposeAsync().ConfigureAwait(false);
                await this.managedMemoryTelemetrySender.DisposeAsync().ConfigureAwait(false);
                await this.memoryStatsTelemetrySender.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.startTelemetryCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.stopTelemetryCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRuntimeStatsCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.workingSetTelemetrySender.DisposeAsync(disposing).ConfigureAwait(false);
                await this.managedMemoryTelemetrySender.DisposeAsync(disposing).ConfigureAwait(false);
                await this.memoryStatsTelemetrySender.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly StartTelemetryCommandInvoker startTelemetryCommandInvoker;
            private readonly StopTelemetryCommandInvoker stopTelemetryCommandInvoker;
            private readonly GetRuntimeStatsCommandInvoker getRuntimeStatsCommandInvoker;
            private readonly WorkingSetTelemetryReceiver workingSetTelemetryReceiver;
            private readonly ManagedMemoryTelemetryReceiver managedMemoryTelemetryReceiver;
            private readonly MemoryStatsTelemetryReceiver memoryStatsTelemetryReceiver;

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

                this.startTelemetryCommandInvoker = new StartTelemetryCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.startTelemetryCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.stopTelemetryCommandInvoker = new StopTelemetryCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.stopTelemetryCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.getRuntimeStatsCommandInvoker = new GetRuntimeStatsCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.getRuntimeStatsCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.workingSetTelemetryReceiver = new WorkingSetTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.workingSetTelemetryReceiver.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.managedMemoryTelemetryReceiver = new ManagedMemoryTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.managedMemoryTelemetryReceiver.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.memoryStatsTelemetryReceiver = new MemoryStatsTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry };
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.memoryStatsTelemetryReceiver.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public StartTelemetryCommandInvoker StartTelemetryCommandInvoker { get => this.startTelemetryCommandInvoker; }
            public StopTelemetryCommandInvoker StopTelemetryCommandInvoker { get => this.stopTelemetryCommandInvoker; }
            public GetRuntimeStatsCommandInvoker GetRuntimeStatsCommandInvoker { get => this.getRuntimeStatsCommandInvoker; }
            public WorkingSetTelemetryReceiver WorkingSetTelemetryReceiver { get => this.workingSetTelemetryReceiver; }
            public ManagedMemoryTelemetryReceiver ManagedMemoryTelemetryReceiver { get => this.managedMemoryTelemetryReceiver; }
            public MemoryStatsTelemetryReceiver MemoryStatsTelemetryReceiver { get => this.memoryStatsTelemetryReceiver; }


            public abstract Task ReceiveTelemetry(string senderId, WorkingSetTelemetry telemetry, IncomingTelemetryMetadata metadata);

            public abstract Task ReceiveTelemetry(string senderId, ManagedMemoryTelemetry telemetry, IncomingTelemetryMetadata metadata);

            public abstract Task ReceiveTelemetry(string senderId, MemoryStatsTelemetry telemetry, IncomingTelemetryMetadata metadata);

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
            public RpcCallAsync<EmptyAvro> StartTelemetryAsync(string executorId, StartTelemetryRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<EmptyAvro>(this.startTelemetryCommandInvoker.InvokeCommandAsync(request, metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
            public RpcCallAsync<EmptyAvro> StopTelemetryAsync(string executorId, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<EmptyAvro>(this.stopTelemetryCommandInvoker.InvokeCommandAsync(new EmptyAvro(), metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
            public RpcCallAsync<GetRuntimeStatsResponsePayload> GetRuntimeStatsAsync(string executorId, GetRuntimeStatsRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<GetRuntimeStatsResponsePayload>(this.getRuntimeStatsCommandInvoker.InvokeCommandAsync(request, metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
                    this.workingSetTelemetryReceiver.StartAsync(cancellationToken),
                    this.managedMemoryTelemetryReceiver.StartAsync(cancellationToken),
                    this.memoryStatsTelemetryReceiver.StartAsync(cancellationToken)).ConfigureAwait(false);
            }

            /// <summary>
            /// Stop accepting telemetry for all telemetry receivers.
            /// </summary>
            /// <param name="cancellationToken">Cancellation token.</param>
            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.workingSetTelemetryReceiver.StopAsync(cancellationToken),
                    this.managedMemoryTelemetryReceiver.StopAsync(cancellationToken),
                    this.memoryStatsTelemetryReceiver.StopAsync(cancellationToken)).ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync()
            {
                await this.startTelemetryCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.stopTelemetryCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.getRuntimeStatsCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.workingSetTelemetryReceiver.DisposeAsync().ConfigureAwait(false);
                await this.managedMemoryTelemetryReceiver.DisposeAsync().ConfigureAwait(false);
                await this.memoryStatsTelemetryReceiver.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.startTelemetryCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.stopTelemetryCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRuntimeStatsCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.workingSetTelemetryReceiver.DisposeAsync(disposing).ConfigureAwait(false);
                await this.managedMemoryTelemetryReceiver.DisposeAsync(disposing).ConfigureAwait(false);
                await this.memoryStatsTelemetryReceiver.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
