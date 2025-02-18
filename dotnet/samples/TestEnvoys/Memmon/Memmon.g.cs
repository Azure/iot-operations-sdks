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

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.startTelemetryCommandExecutor = new StartTelemetryCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = StartTelemetryInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.stopTelemetryCommandExecutor = new StopTelemetryCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = StopTelemetryInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRuntimeStatsCommandExecutor = new GetRuntimeStatsCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = GetRuntimeStatsInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.workingSetTelemetrySender = new WorkingSetTelemetrySender(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.managedMemoryTelemetrySender = new ManagedMemoryTelemetrySender(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.memoryStatsTelemetrySender = new MemoryStatsTelemetrySender(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public StartTelemetryCommandExecutor StartTelemetryCommandExecutor { get => this.startTelemetryCommandExecutor; }
            public StopTelemetryCommandExecutor StopTelemetryCommandExecutor { get => this.stopTelemetryCommandExecutor; }
            public GetRuntimeStatsCommandExecutor GetRuntimeStatsCommandExecutor { get => this.getRuntimeStatsCommandExecutor; }
            public WorkingSetTelemetrySender WorkingSetTelemetrySender { get => this.workingSetTelemetrySender; }
            public ManagedMemoryTelemetrySender ManagedMemoryTelemetrySender { get => this.managedMemoryTelemetrySender; }
            public MemoryStatsTelemetrySender MemoryStatsTelemetrySender { get => this.memoryStatsTelemetrySender; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<CommandResponseMetadata?> StartTelemetryAsync(StartTelemetryRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsAsync(GetRuntimeStatsRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task SendTelemetryAsync(WorkingSetTelemetry telemetry, OutgoingTelemetryMetadata metadata, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.workingSetTelemetrySender.SendTelemetryAsync(telemetry, metadata, transientTopicTokenMap, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task SendTelemetryAsync(ManagedMemoryTelemetry telemetry, OutgoingTelemetryMetadata metadata, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.managedMemoryTelemetrySender.SendTelemetryAsync(telemetry, metadata, transientTopicTokenMap, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task SendTelemetryAsync(MemoryStatsTelemetry telemetry, OutgoingTelemetryMetadata metadata, IReadOnlyDictionary<string, string>? transientTopicTokenMap = null, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.memoryStatsTelemetrySender.SendTelemetryAsync(telemetry, metadata, transientTopicTokenMap, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                Dictionary<string, string>? transientTopicTokenMap = new()
                {
                    { "executorId", clientId },
                };

                await Task.WhenAll(
                    this.startTelemetryCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.stopTelemetryCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.getRuntimeStatsCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.startTelemetryCommandExecutor.StopAsync(cancellationToken),
                    this.stopTelemetryCommandExecutor.StopAsync(cancellationToken),
                    this.getRuntimeStatsCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<EmptyAvro>> StartTelemetryInt(ExtendedRequest<StartTelemetryRequestPayload> req, IReadOnlyDictionary<string, string> topicTokenMap, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StartTelemetryAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<EmptyAvro>> StopTelemetryInt(ExtendedRequest<EmptyAvro> req, IReadOnlyDictionary<string, string> topicTokenMap, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StopTelemetryAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<GetRuntimeStatsResponsePayload>> GetRuntimeStatsInt(ExtendedRequest<GetRuntimeStatsRequestPayload> req, IReadOnlyDictionary<string, string> topicTokenMap, CancellationToken cancellationToken)
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

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.startTelemetryCommandInvoker = new StartTelemetryCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.stopTelemetryCommandInvoker = new StopTelemetryCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRuntimeStatsCommandInvoker = new GetRuntimeStatsCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.workingSetTelemetryReceiver = new WorkingSetTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.managedMemoryTelemetryReceiver = new ManagedMemoryTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.memoryStatsTelemetryReceiver = new MemoryStatsTelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public StartTelemetryCommandInvoker StartTelemetryCommandInvoker { get => this.startTelemetryCommandInvoker; }
            public StopTelemetryCommandInvoker StopTelemetryCommandInvoker { get => this.stopTelemetryCommandInvoker; }
            public GetRuntimeStatsCommandInvoker GetRuntimeStatsCommandInvoker { get => this.getRuntimeStatsCommandInvoker; }
            public WorkingSetTelemetryReceiver WorkingSetTelemetryReceiver { get => this.workingSetTelemetryReceiver; }
            public ManagedMemoryTelemetryReceiver ManagedMemoryTelemetryReceiver { get => this.managedMemoryTelemetryReceiver; }
            public MemoryStatsTelemetryReceiver MemoryStatsTelemetryReceiver { get => this.memoryStatsTelemetryReceiver; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task ReceiveTelemetry(string senderId, WorkingSetTelemetry telemetry, IncomingTelemetryMetadata metadata);

            public abstract Task ReceiveTelemetry(string senderId, ManagedMemoryTelemetry telemetry, IncomingTelemetryMetadata metadata);

            public abstract Task ReceiveTelemetry(string senderId, MemoryStatsTelemetry telemetry, IncomingTelemetryMetadata metadata);

            public RpcCallAsync<EmptyAvro> StartTelemetryAsync(string executorId, StartTelemetryRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                Dictionary<string, string>? transientTopicTokenMap = new()
                {
                    { "invokerClientId", clientId },
                    { "executorId", executorId },
                };

                return new RpcCallAsync<EmptyAvro>(this.startTelemetryCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<EmptyAvro> StopTelemetryAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                Dictionary<string, string>? transientTopicTokenMap = new()
                {
                    { "invokerClientId", clientId },
                    { "executorId", executorId },
                };

                return new RpcCallAsync<EmptyAvro>(this.stopTelemetryCommandInvoker.InvokeCommandAsync(new EmptyAvro(), metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetRuntimeStatsResponsePayload> GetRuntimeStatsAsync(string executorId, GetRuntimeStatsRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                Dictionary<string, string>? transientTopicTokenMap = new()
                {
                    { "invokerClientId", clientId },
                    { "executorId", executorId },
                };

                return new RpcCallAsync<GetRuntimeStatsResponsePayload>(this.getRuntimeStatsCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.workingSetTelemetryReceiver.StartAsync(cancellationToken),
                    this.managedMemoryTelemetryReceiver.StartAsync(cancellationToken),
                    this.memoryStatsTelemetryReceiver.StartAsync(cancellationToken)).ConfigureAwait(false);
            }

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
