/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace TestEnvoys.Passthrough
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
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class Passthrough
    {
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly PassCommandExecutor passCommandExecutor;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.passCommandExecutor = new PassCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = PassInt};
                this.passCommandExecutor.TopicTokenMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            public PassCommandExecutor PassCommandExecutor { get => this.passCommandExecutor; }


            public abstract Task<ExtendedResponse<byte[]>> PassAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
                    this.passCommandExecutor.StartAsync(preferredDispatchConcurrency, topicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.passCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<byte[]>> PassInt(ExtendedRequest<byte[]> req, CancellationToken cancellationToken)
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
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly PassCommandInvoker passCommandInvoker;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.passCommandInvoker = new PassCommandInvoker(applicationContext, mqttClient);
                this.passCommandInvoker.TopicTokenMap.Concat(topicTokenMap ?? new Dictionary<string, string>()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            public PassCommandInvoker PassCommandInvoker { get => this.passCommandInvoker; }


            public RpcCallAsync<byte[]> PassAsync(string executorId, byte[] request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? topicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                topicTokenMap ??= new();

                topicTokenMap["invokerClientId"] = clientId;
                topicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<byte[]>(this.passCommandInvoker.InvokeCommandAsync(request, metadata, topicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
