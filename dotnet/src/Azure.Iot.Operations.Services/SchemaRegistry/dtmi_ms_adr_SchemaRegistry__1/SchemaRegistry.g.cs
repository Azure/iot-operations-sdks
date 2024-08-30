/* This is an auto-generated file.  Do not modify. */

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

    [ModelId("dtmi:ms:adr:SchemaRegistry;1")]
    [CommandTopic("adr/{modelId}/{commandName}")]
    [ServiceGroupId("MyServiceGroup")]
    public static partial class SchemaRegistry
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly PutCommandExecutor putCommandExecutor;
            private readonly GetCommandExecutor getCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.putCommandExecutor = new PutCommandExecutor(mqttClient) { OnCommandReceived = Put_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getCommandExecutor = new GetCommandExecutor(mqttClient) { OnCommandReceived = Get_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PutCommandExecutor PutCommandExecutor { get => this.putCommandExecutor; }
            public GetCommandExecutor GetCommandExecutor { get => this.getCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<PutCommandResponse>> PutAsync(PutCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<GetCommandResponse>> GetAsync(GetCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.putCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken),
                    this.getCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.putCommandExecutor.StopAsync(cancellationToken),
                    this.getCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<PutCommandResponse>> Put_Int(ExtendedRequest<PutCommandRequest> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<PutCommandResponse> extended = await this.PutAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<PutCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<GetCommandResponse>> Get_Int(ExtendedRequest<GetCommandRequest> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<GetCommandResponse> extended = await this.GetAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<GetCommandResponse> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
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
            private readonly PutCommandInvoker putCommandInvoker;
            private readonly GetCommandInvoker getCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.putCommandInvoker = new PutCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
                this.getCommandInvoker = new GetCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PutCommandInvoker PutCommandInvoker { get => this.putCommandInvoker; }
            public GetCommandInvoker GetCommandInvoker { get => this.getCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<PutCommandResponse> PutAsync(PutCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<PutCommandResponse>(this.putCommandInvoker.InvokeCommandAsync("", request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<GetCommandResponse> GetAsync(GetCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<GetCommandResponse>(this.getCommandInvoker.InvokeCommandAsync("", request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
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
