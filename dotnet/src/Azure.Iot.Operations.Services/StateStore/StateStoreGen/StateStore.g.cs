/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.StateStore.dtmi_ms_aio_mq_StateStore__1
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
    using Azure.Iot.Operations.Services.StateStore;

    [CommandTopic("statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.6.0.0")]
    public static partial class StateStore
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private IMqttPubSubClient mqttClient;
            private readonly InvokeCommandExecutor invokeCommandExecutor;

            public Service(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.invokeCommandExecutor = new InvokeCommandExecutor(mqttClient) { OnCommandReceived = Invoke_Int, CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public InvokeCommandExecutor InvokeCommandExecutor { get => this.invokeCommandExecutor; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public abstract Task<ExtendedResponse<byte[]>> InvokeAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
                    this.invokeCommandExecutor.StartAsync(preferredDispatchConcurrency, transientTopicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.invokeCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<byte[]>> Invoke_Int(ExtendedRequest<byte[]> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<byte[]> extended = await this.InvokeAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<byte[]> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.invokeCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.invokeCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private IMqttPubSubClient mqttClient;
            private readonly InvokeCommandInvoker invokeCommandInvoker;

            public Client(IMqttPubSubClient mqttClient)
            {
                this.mqttClient = mqttClient;
                this.CustomTopicTokenMap = new();

                this.invokeCommandInvoker = new InvokeCommandInvoker(mqttClient) { CustomTopicTokenMap = this.CustomTopicTokenMap };
            }

            public InvokeCommandInvoker InvokeCommandInvoker { get => this.invokeCommandInvoker; }

            public Dictionary<string, string> CustomTopicTokenMap { get; private init; }

            public RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
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

                return new RpcCallAsync<byte[]>(this.invokeCommandInvoker.InvokeCommandAsync(request, metadata, transientTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.invokeCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.invokeCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
