/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1
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
    using Azure.Iot.Operations.Services.SchemaRegistry;

    [CommandTopic("adr/{modelId}/{commandName}")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public static partial class SchemaRegistry
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private IMqttPubSubClient mqttClient;
            private readonly PutCommandExecutor putCommandExecutor;
            private readonly GetCommandExecutor getCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.putCommandExecutor = new PutCommandExecutor(mqttClient) { OnCommandReceived = Put_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getCommandExecutor = new GetCommandExecutor(mqttClient) { OnCommandReceived = Get_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PutCommandExecutor PutCommandExecutor { get => this.putCommandExecutor; }
            public GetCommandExecutor GetCommandExecutor { get => this.getCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<PutResponsePayload>> PutAsync(PutRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetResponsePayload>> GetAsync(GetRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
                    this.putCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken),
                    this.getCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.putCommandExecutor.StopAsync(cancellationToken),
                    this.getCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<PutResponsePayload>> Put_Int(ExtendedRequest<PutRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<PutResponsePayload> extended = await this.PutAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<PutResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<GetResponsePayload>> Get_Int(ExtendedRequest<GetRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetResponsePayload> extended = await this.GetAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.putCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.getCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.putCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private IMqttPubSubClient mqttClient;
            private readonly PutCommandInvoker putCommandInvoker;
            private readonly GetCommandInvoker getCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.putCommandInvoker = new PutCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getCommandInvoker = new GetCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PutCommandInvoker PutCommandInvoker { get => this.putCommandInvoker; }
            public GetCommandInvoker GetCommandInvoker { get => this.getCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<PutResponsePayload> PutAsync(PutRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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
                };

                return new RpcCallAsync<PutResponsePayload>(this.putCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetResponsePayload> GetAsync(GetRequestPayload request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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
                };

                return new RpcCallAsync<GetResponsePayload>(this.getCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.putCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.getCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.putCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.getCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
