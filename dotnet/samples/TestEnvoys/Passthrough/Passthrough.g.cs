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

            /// <summary>
            /// Construct a new instance of this service.
            /// </summary>
            /// <param name="applicationContext">The shared context for your application.</param>
            /// <param name="mqttClient">The MQTT client to use.</param>
            /// <param name="topicTokenMap">
            /// The topic token replacement map to use for all operations by default. Generally, this will include the token values
            /// for topic tokens such as "modelId" which should be the same for the duration of this service's lifetime. Note that
            /// additional topic tokens can be specified when starting the service with <see cref="StartAsync(Dictionary{string, string}?, int?, CancellationToken)"/> and
            /// can be specified per-telemetry message.
            /// </param>
            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.passCommandExecutor = new PassCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = PassInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.passCommandExecutor.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public PassCommandExecutor PassCommandExecutor { get => this.passCommandExecutor; }


            public abstract Task<ExtendedResponse<byte[]>> PassAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            /// <summary>
            /// Begin accepting command invocations for all command executors.
            /// </summary>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacements to use in addition to any topic tokens specified in the constructor. If this map
            /// contains any keys that topic tokens provided in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="preferredDispatchConcurrency">The dispatch concurrency count for the command response cache to use.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <remarks>
            /// Specifying custom topic tokens in <paramref name="additionalTopicTokenMap"/> allows you to make command executors only
            /// accept commands over a specific topic.
            ///
            /// Note that a given command executor can only be started with one set of topic token replacements. If you want a command executor
            /// to only handle commands for several specific sets of topic token values (as opposed to all possible topic token values), then you will
            /// instead need to create a command executor per topic token set.
            /// </remarks>
            public async Task StartAsync(Dictionary<string, string>? additionalTopicTokenMap = null, int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
            {
                additionalTopicTokenMap ??= new();
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before starting service.");
                }

                additionalTopicTokenMap["executorId"] = clientId;

                await Task.WhenAll(
                    this.passCommandExecutor.StartAsync(additionalTopicTokenMap, preferredDispatchConcurrency, cancellationToken)).ConfigureAwait(false);
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

            /// <summary>
            /// Construct a new instance of this client.
            /// </summary>
            /// <param name="applicationContext">The shared context for your application.</param>
            /// <param name="mqttClient">The MQTT client to use.</param>
            /// <param name="topicTokenMap">
            /// The topic token replacement map to use for all operations by default. Generally, this will include the token values
            /// for topic tokens such as "modelId" which should be the same for the duration of this client's lifetime. Note that
            /// additional topic tokens can be specified when starting the client with <see cref="StartAsync(Dictionary{string, string}?, int?, CancellationToken)"/>.
            /// </param>
            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.passCommandInvoker = new PassCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.passCommandInvoker.TopicTokenMap.TryAdd("ex:" + topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public PassCommandInvoker PassCommandInvoker { get => this.passCommandInvoker; }


            /// <summary>
            /// Invoke a command.
            /// </summary>
            /// <param name="requestMetadata">The metadata for this command request.</param>
            /// <param name="additionalTopicTokenMap">
            /// The topic token replacement map to use in addition to the topic tokens specified in the constructor. If this map
            /// contains any keys that the topic tokens specified in the constructor also has, then values specified in this map will take precedence.
            /// </param>
            /// <param name="commandTimeout">How long the command will be available on the broker for an executor to receive.</param>
            /// <param name="cancellationToken">Cancellation token.</param>
            /// <returns>The command response.</returns>
            public RpcCallAsync<byte[]> PassAsync(string executorId, byte[] request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                Dictionary<string, string> prefixedAdditionalTopicTokenMap = new();
                foreach (string key in additionalTopicTokenMap.Keys)
                {
                    prefixedAdditionalTopicTokenMap["ex:" + key] = additionalTopicTokenMap[key];
                }

                prefixedAdditionalTopicTokenMap["invokerClientId"] = clientId;
                prefixedAdditionalTopicTokenMap["executorId"] = executorId;

                return new RpcCallAsync<byte[]>(this.passCommandInvoker.InvokeCommandAsync(request, metadata, prefixedAdditionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
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
