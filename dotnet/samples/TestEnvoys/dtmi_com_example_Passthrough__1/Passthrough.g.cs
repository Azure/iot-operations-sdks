/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_com_example_Passthrough__1
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

    [ModelId("dtmi:com:example:Passthrough;1")]
    [CommandTopic("rpc/command-samples/{executorId}/{commandName}")]
    public static partial class Passthrough
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private readonly PassCommandExecutor passCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.passCommandExecutor = new PassCommandExecutor(mqttClient) { OnCommandReceived = Pass_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PassCommandExecutor PassCommandExecutor { get => this.passCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<byte[]>> PassAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.passCommandExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.passCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<byte[]>> Pass_Int(ExtendedRequest<byte[]> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<byte[]> extended = await this.PassAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<byte[]> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.passCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.passCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private readonly PassCommandInvoker passCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.CustomTopicTokenMap = new();

                this.passCommandInvoker = new PassCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public PassCommandInvoker PassCommandInvoker { get => this.passCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<byte[]> PassAsync(string executorId, byte[] request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                return new RpcCallAsync<byte[]>(this.passCommandInvoker.InvokeCommandAsync(executorId, request, metadata, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.passCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.passCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
