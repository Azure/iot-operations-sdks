/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Counter__1
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

    [ModelId("dtmi:com:example:Counter;1")]
    [CommandTopic("rpc/command-samples/{executorId}/{commandName}")]
    public static partial class Counter
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly ReadCounterCommandExecutor readCounterCommandExecutor;
            private readonly IncrementCommandExecutor incrementCommandExecutor;
            private readonly ResetCommandExecutor resetCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.readCounterCommandExecutor = new ReadCounterCommandExecutor(mqttClient) { OnCommandReceived = ReadCounter_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.incrementCommandExecutor = new IncrementCommandExecutor(mqttClient) { OnCommandReceived = Increment_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.resetCommandExecutor = new ResetCommandExecutor(mqttClient) { OnCommandReceived = Reset_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public ReadCounterCommandExecutor ReadCounterCommandExecutor { get => this.readCounterCommandExecutor; }
            public IncrementCommandExecutor IncrementCommandExecutor { get => this.incrementCommandExecutor; }
            public ResetCommandExecutor ResetCommandExecutor { get => this.resetCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<ReadCounterCommandResponse>> ReadCounterAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<IncrementCommandResponse>> IncrementAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<CommandResponseMetadata?> ResetAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
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
            private async Task<ExtendedResponse<ReadCounterCommandResponse>> ReadCounter_Int(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<ReadCounterCommandResponse> extended = await this.ReadCounterAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<ReadCounterCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<IncrementCommandResponse>> Increment_Int(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<IncrementCommandResponse> extended = await this.IncrementAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<IncrementCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<EmptyJson>> Reset_Int(ExtendedRequest<EmptyJson> req, CancellationToken cancellationToken)
            {
                CommandResponseMetadata? responseMetadata = await this.ResetAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<EmptyJson> { ResponseMetadata = responseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.readCounterCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.incrementCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.resetCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCounterCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.incrementCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.resetCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private readonly ReadCounterCommandInvoker readCounterCommandInvoker;
            private readonly IncrementCommandInvoker incrementCommandInvoker;
            private readonly ResetCommandInvoker resetCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.readCounterCommandInvoker = new ReadCounterCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.incrementCommandInvoker = new IncrementCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.resetCommandInvoker = new ResetCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public ReadCounterCommandInvoker ReadCounterCommandInvoker { get => this.readCounterCommandInvoker; }
            public IncrementCommandInvoker IncrementCommandInvoker { get => this.incrementCommandInvoker; }
            public ResetCommandInvoker ResetCommandInvoker { get => this.resetCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<ReadCounterCommandResponse> ReadCounterAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<ReadCounterCommandResponse>(this.readCounterCommandInvoker.InvokeCommandAsync(executorId, new EmptyJson(), metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<IncrementCommandResponse> IncrementAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<IncrementCommandResponse>(this.incrementCommandInvoker.InvokeCommandAsync(executorId, new EmptyJson(), metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<EmptyJson> ResetAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<EmptyJson>(this.resetCommandInvoker.InvokeCommandAsync(executorId, new EmptyJson(), metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.readCounterCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.incrementCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.resetCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.readCounterCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.incrementCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.resetCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
