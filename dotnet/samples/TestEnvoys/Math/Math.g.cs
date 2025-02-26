/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Math
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
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class Math
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly IsPrimeCommandExecutor isPrimeCommandExecutor;
            private readonly FibCommandExecutor fibCommandExecutor;
            private readonly GetRandomCommandExecutor getRandomCommandExecutor;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.isPrimeCommandExecutor = new IsPrimeCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = IsPrimeInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.fibCommandExecutor = new FibCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = FibInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRandomCommandExecutor = new GetRandomCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = GetRandomInt, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public IsPrimeCommandExecutor IsPrimeCommandExecutor { get => this.isPrimeCommandExecutor; }
            public FibCommandExecutor FibCommandExecutor { get => this.fibCommandExecutor; }
            public GetRandomCommandExecutor GetRandomCommandExecutor { get => this.getRandomCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<IsPrimeResponsePayload>> IsPrimeAsync(IsPrimeRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<FibResponsePayload>> FibAsync(FibRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetRandomResponsePayload>> GetRandomAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
                    this.isPrimeCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.fibCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.getRandomCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.isPrimeCommandExecutor.StopAsync(cancellationToken),
                    this.fibCommandExecutor.StopAsync(cancellationToken),
                    this.getRandomCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<IsPrimeResponsePayload>> IsPrimeInt(ExtendedRequest<IsPrimeRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<IsPrimeResponsePayload> extended = await this.IsPrimeAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<IsPrimeResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<FibResponsePayload>> FibInt(ExtendedRequest<FibRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<FibResponsePayload> extended = await this.FibAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<FibResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<GetRandomResponsePayload>> GetRandomInt(ExtendedRequest<Google.Protobuf.WellKnownTypes.Empty> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetRandomResponsePayload> extended = await this.GetRandomAsync(req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetRandomResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
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
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly IsPrimeCommandInvoker isPrimeCommandInvoker;
            private readonly FibCommandInvoker fibCommandInvoker;
            private readonly GetRandomCommandInvoker getRandomCommandInvoker;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.isPrimeCommandInvoker = new IsPrimeCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.fibCommandInvoker = new FibCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getRandomCommandInvoker = new GetRandomCommandInvoker(applicationContext, mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public IsPrimeCommandInvoker IsPrimeCommandInvoker { get => this.isPrimeCommandInvoker; }
            public FibCommandInvoker FibCommandInvoker { get => this.fibCommandInvoker; }
            public GetRandomCommandInvoker GetRandomCommandInvoker { get => this.getRandomCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<IsPrimeResponsePayload> IsPrimeAsync(string executorId, IsPrimeRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<IsPrimeResponsePayload>(this.isPrimeCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<FibResponsePayload> FibAsync(string executorId, FibRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<FibResponsePayload>(this.fibCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetRandomResponsePayload> GetRandomAsync(string executorId, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<GetRandomResponsePayload>(this.getRandomCommandInvoker.InvokeCommandAsync(new Google.Protobuf.WellKnownTypes.Empty(), metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
