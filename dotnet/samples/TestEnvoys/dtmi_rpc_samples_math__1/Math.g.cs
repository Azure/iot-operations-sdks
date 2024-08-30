/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_rpc_samples_math__1
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

    [ModelId("dtmi:rpc:samples:math;1")]
    [CommandTopic("rpc/samples/{modelId}/{executorId}/{commandName}")]
    public static partial class Math
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly IsPrimeCommandExecutor isPrimeCommandExecutor;
            private readonly FibCommandExecutor fibCommandExecutor;
            private readonly GetRandomCommandExecutor getRandomCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.isPrimeCommandExecutor = new IsPrimeCommandExecutor(mqttClient) { OnCommandReceived = IsPrime_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.fibCommandExecutor = new FibCommandExecutor(mqttClient) { OnCommandReceived = Fib_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRandomCommandExecutor = new GetRandomCommandExecutor(mqttClient) { OnCommandReceived = GetRandom_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public IsPrimeCommandExecutor IsPrimeCommandExecutor { get => this.isPrimeCommandExecutor; }
            public FibCommandExecutor FibCommandExecutor { get => this.fibCommandExecutor; }
            public GetRandomCommandExecutor GetRandomCommandExecutor { get => this.getRandomCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<IsPrimeCommandResponse>> IsPrimeAsync(IsPrimeCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<FibCommandResponse>> FibAsync(FibCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetRandomCommandResponse>> GetRandomAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.isPrimeCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.fibCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.getRandomCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.isPrimeCommandExecutor.StopAsync(cancellationToken),
                    this.fibCommandExecutor.StopAsync(cancellationToken),
                    this.getRandomCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<IsPrimeCommandResponse>> IsPrime_Int(ExtendedRequest<IsPrimeCommandRequest> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<IsPrimeCommandResponse> extended = await this.IsPrimeAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<IsPrimeCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<FibCommandResponse>> Fib_Int(ExtendedRequest<FibCommandRequest> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<FibCommandResponse> extended = await this.FibAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<FibCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<GetRandomCommandResponse>> GetRandom_Int(ExtendedRequest<Google.Protobuf.WellKnownTypes.Empty> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetRandomCommandResponse> extended = await this.GetRandomAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetRandomCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.isPrimeCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.fibCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.getRandomCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.isPrimeCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.fibCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRandomCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private readonly IsPrimeCommandInvoker isPrimeCommandInvoker;
            private readonly FibCommandInvoker fibCommandInvoker;
            private readonly GetRandomCommandInvoker getRandomCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.isPrimeCommandInvoker = new IsPrimeCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.fibCommandInvoker = new FibCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRandomCommandInvoker = new GetRandomCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public IsPrimeCommandInvoker IsPrimeCommandInvoker { get => this.isPrimeCommandInvoker; }
            public FibCommandInvoker FibCommandInvoker { get => this.fibCommandInvoker; }
            public GetRandomCommandInvoker GetRandomCommandInvoker { get => this.getRandomCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<IsPrimeCommandResponse> IsPrimeAsync(string executorId, IsPrimeCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<IsPrimeCommandResponse>(this.isPrimeCommandInvoker.InvokeCommandAsync(executorId, request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<FibCommandResponse> FibAsync(string executorId, FibCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<FibCommandResponse>(this.fibCommandInvoker.InvokeCommandAsync(executorId, request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetRandomCommandResponse> GetRandomAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<GetRandomCommandResponse>(this.getRandomCommandInvoker.InvokeCommandAsync(executorId, new Google.Protobuf.WellKnownTypes.Empty(), metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.isPrimeCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.fibCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.getRandomCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.isPrimeCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.fibCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getRandomCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
