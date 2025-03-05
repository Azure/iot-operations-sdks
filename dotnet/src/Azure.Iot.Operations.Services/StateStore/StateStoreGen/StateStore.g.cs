/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.StateStore.StateStore
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
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class StateStore
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly InvokeCommandExecutor invokeCommandExecutor;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.invokeCommandExecutor = new InvokeCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = InvokeInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.invokeCommandInvoker.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
            }

            public InvokeCommandExecutor InvokeCommandExecutor { get => this.invokeCommandExecutor; }


            public abstract Task<ExtendedResponse<byte[]>> InvokeAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public async Task StartAsync(Dictionary<string, string>? topicTokenMap = null, int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                topicTokenMap ??= new();
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                topicTokenMap["executorId"] = clientId;

                await Task.WhenAll(
                    this.invokeCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.invokeCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<byte[]>> InvokeInt(ExtendedRequest<byte[]> req, CancellationToken cancellationToken)
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
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly InvokeCommandInvoker invokeCommandInvoker;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.invokeCommandInvoker = new InvokeCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap)
                    {
                        this.invokeCommandInvoker.TopicTokenMap[topicTokenKey] = topicTokenMap[topicTokenKey];
                    }
                }
            }

            public InvokeCommandInvoker InvokeCommandInvoker { get => this.invokeCommandInvoker; }


            public RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();

                topicTokenMap["invokerClientId"] = clientId;

                return new RpcCallAsync<byte[]>(this.invokeCommandInvoker.InvokeCommandAsync(request, metadata, topicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
