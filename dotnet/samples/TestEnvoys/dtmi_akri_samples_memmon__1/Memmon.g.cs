/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_akri_samples_memmon__1
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

    [ModelId("dtmi:akri:samples:memmon;1")]
    [CommandTopic("rpc/samples/{modelId}/{executorId}/{commandName}")]
    [TelemetryTopic("rpc/samples/{modelId}/{senderId}/{telemetryName}")]
    public static partial class Memmon
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly StartTelemetryCommandExecutor startTelemetryCommandExecutor;
            private readonly StopTelemetryCommandExecutor stopTelemetryCommandExecutor;
            private readonly GetRuntimeStatsCommandExecutor getRuntimeStatsCommandExecutor;
            private readonly WorkingSetTelemetrySender workingSetTelemetrySender;
            private readonly ManagedMemoryTelemetrySender managedMemoryTelemetrySender;
            private readonly MemoryStatsTelemetrySender memoryStatsTelemetrySender;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.startTelemetryCommandExecutor = new StartTelemetryCommandExecutor(mqttClient) { OnCommandReceived = StartTelemetry_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.stopTelemetryCommandExecutor = new StopTelemetryCommandExecutor(mqttClient) { OnCommandReceived = StopTelemetry_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRuntimeStatsCommandExecutor = new GetRuntimeStatsCommandExecutor(mqttClient) { OnCommandReceived = GetRuntimeStats_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.workingSetTelemetrySender = new WorkingSetTelemetrySender(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.managedMemoryTelemetrySender = new ManagedMemoryTelemetrySender(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.memoryStatsTelemetrySender = new MemoryStatsTelemetrySender(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public StartTelemetryCommandExecutor StartTelemetryCommandExecutor { get => this.startTelemetryCommandExecutor; }
            public StopTelemetryCommandExecutor StopTelemetryCommandExecutor { get => this.stopTelemetryCommandExecutor; }
            public GetRuntimeStatsCommandExecutor GetRuntimeStatsCommandExecutor { get => this.getRuntimeStatsCommandExecutor; }
            public WorkingSetTelemetrySender WorkingSetTelemetrySender { get => this.workingSetTelemetrySender; }
            public ManagedMemoryTelemetrySender ManagedMemoryTelemetrySender { get => this.managedMemoryTelemetrySender; }
            public MemoryStatsTelemetrySender MemoryStatsTelemetrySender { get => this.memoryStatsTelemetrySender; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<CommandResponseMetadata?> StartTelemetryAsync(StartTelemetryCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> StopTelemetryAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetRuntimeStatsCommandResponse>> GetRuntimeStatsAsync(GetRuntimeStatsCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task SendTelemetryAsync(WorkingSetTelemetry telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.workingSetTelemetrySender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task SendTelemetryAsync(ManagedMemoryTelemetry telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.managedMemoryTelemetrySender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task SendTelemetryAsync(MemoryStatsTelemetry telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.memoryStatsTelemetrySender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
            }

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
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
            private async Task<ExtendedResponse<EmptyAvro>> StartTelemetry_Int(ExtendedRequest<StartTelemetryCommandRequest> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StartTelemetryAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<EmptyAvro>> StopTelemetry_Int(ExtendedRequest<EmptyAvro> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.StopTelemetryAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyAvro> { ResponseMetadata = responseMetadata };
            }
            private async Task<ExtendedResponse<GetRuntimeStatsCommandResponse>> GetRuntimeStats_Int(ExtendedRequest<GetRuntimeStatsCommandRequest> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetRuntimeStatsCommandResponse> extended = await this.GetRuntimeStatsAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetRuntimeStatsCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.startTelemetryCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.stopTelemetryCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.getRuntimeStatsCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.startTelemetryCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.stopTelemetryCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRuntimeStatsCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private readonly StartTelemetryCommandInvoker startTelemetryCommandInvoker;
            private readonly StopTelemetryCommandInvoker stopTelemetryCommandInvoker;
            private readonly GetRuntimeStatsCommandInvoker getRuntimeStatsCommandInvoker;
            private readonly WorkingSetTelemetryReceiver workingSetTelemetryReceiver;
            private readonly ManagedMemoryTelemetryReceiver managedMemoryTelemetryReceiver;
            private readonly MemoryStatsTelemetryReceiver memoryStatsTelemetryReceiver;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.startTelemetryCommandInvoker = new StartTelemetryCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.stopTelemetryCommandInvoker = new StopTelemetryCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRuntimeStatsCommandInvoker = new GetRuntimeStatsCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.workingSetTelemetryReceiver = new WorkingSetTelemetryReceiver(mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.managedMemoryTelemetryReceiver = new ManagedMemoryTelemetryReceiver(mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.memoryStatsTelemetryReceiver = new MemoryStatsTelemetryReceiver(mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
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

            public RpcCallAsync<EmptyAvro> StartTelemetryAsync(string executorId, StartTelemetryCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<EmptyAvro>(this.startTelemetryCommandInvoker.InvokeCommandAsync(executorId, request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<EmptyAvro> StopTelemetryAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<EmptyAvro>(this.stopTelemetryCommandInvoker.InvokeCommandAsync(executorId, new EmptyAvro(), metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetRuntimeStatsCommandResponse> GetRuntimeStatsAsync(string executorId, GetRuntimeStatsCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<GetRuntimeStatsCommandResponse>(this.getRuntimeStatsCommandInvoker.InvokeCommandAsync(executorId, request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
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
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.startTelemetryCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.stopTelemetryCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRuntimeStatsCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
