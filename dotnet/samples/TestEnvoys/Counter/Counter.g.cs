/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

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
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
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

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.readCounterCommandExecutor = new ReadCounterCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ReadCounterInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.incrementCommandExecutor = new IncrementCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = IncrementInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.resetCommandExecutor = new ResetCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = ResetInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.telemetrySender = new TelemetrySender(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public ReadCounterCommandExecutor ReadCounterCommandExecutor { get => this.readCounterCommandExecutor; }
            public IncrementCommandExecutor IncrementCommandExecutor { get => this.incrementCommandExecutor; }
            public ResetCommandExecutor ResetCommandExecutor { get => this.resetCommandExecutor; }
            public TelemetrySender TelemetrySender { get => this.telemetrySender; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<ReadCounterResponsePayload>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<IncrementResponsePayload>> IncrementAsync(IncrementRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task SendTelemetryAsync(TelemetryCollection telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default)
            {
                await this.telemetrySender.SendTelemetryAsync(telemetry, metadata, qos, messageExpiryInterval, cancellationToken);
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
                    this.readCounterCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.incrementCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.resetCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken)).ConfigureAwait(false);
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

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.readCounterCommandInvoker = new ReadCounterCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.incrementCommandInvoker = new IncrementCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.resetCommandInvoker = new ResetCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.telemetryReceiver = new TelemetryReceiver(applicationContext, mqttClient) { OnTelemetryReceived = this.ReceiveTelemetry, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public ReadCounterCommandInvoker ReadCounterCommandInvoker { get => this.readCounterCommandInvoker; }
            public IncrementCommandInvoker IncrementCommandInvoker { get => this.incrementCommandInvoker; }
            public ResetCommandInvoker ResetCommandInvoker { get => this.resetCommandInvoker; }
            public TelemetryReceiver TelemetryReceiver { get => this.telemetryReceiver; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata);

            public RpcCallAsync<ReadCounterResponsePayload> ReadCounterAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<ReadCounterResponsePayload>(this.readCounterCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<IncrementResponsePayload> IncrementAsync(string executorId, IncrementRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<IncrementResponsePayload>(this.incrementCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<EmptyJson> ResetAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<EmptyJson>(this.resetCommandInvoker.InvokeCommandAsync(new EmptyJson(), metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async Task StartAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.telemetryReceiver.StartAsync(cancellationToken)).ConfigureAwait(false);
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
