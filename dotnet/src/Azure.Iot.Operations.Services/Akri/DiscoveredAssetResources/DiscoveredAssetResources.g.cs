/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
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
    using Azure.Iot.Operations.Services.Akri;

    [CommandTopic("akri/discovery/{modelId}/{invokerClientId}/command/{commandName}")]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.9.0.0")]
    public static partial class DiscoveredAssetResources
    {
        [ServiceGroupId("MyServiceGroup")]
        public abstract partial class Service : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly CreateDiscoveredAssetEndpointProfileCommandExecutor createDiscoveredAssetEndpointProfileCommandExecutor;
            private readonly CreateDiscoveredAssetCommandExecutor createDiscoveredAssetCommandExecutor;

            public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.createDiscoveredAssetEndpointProfileCommandExecutor = new CreateDiscoveredAssetEndpointProfileCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = CreateDiscoveredAssetEndpointProfileInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.createDiscoveredAssetEndpointProfileCommandExecutor.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.createDiscoveredAssetCommandExecutor = new CreateDiscoveredAssetCommandExecutor(applicationContext, mqttClient) { OnCommandReceived = CreateDiscoveredAssetInt};
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.createDiscoveredAssetCommandExecutor.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public CreateDiscoveredAssetEndpointProfileCommandExecutor CreateDiscoveredAssetEndpointProfileCommandExecutor { get => this.createDiscoveredAssetEndpointProfileCommandExecutor; }
            public CreateDiscoveredAssetCommandExecutor CreateDiscoveredAssetCommandExecutor { get => this.createDiscoveredAssetCommandExecutor; }


            public abstract Task<ExtendedResponse<CreateDiscoveredAssetEndpointProfileResponsePayload>> CreateDiscoveredAssetEndpointProfileAsync(CreateDiscoveredAssetEndpointProfileRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

            public abstract Task<ExtendedResponse<CreateDiscoveredAssetResponsePayload>> CreateDiscoveredAssetAsync(CreateDiscoveredAssetRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);

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
                    this.createDiscoveredAssetEndpointProfileCommandExecutor.StartAsync(preferredDispatchConcurrency, additionalTopicTokenMap, cancellationToken),
                    this.createDiscoveredAssetCommandExecutor.StartAsync(preferredDispatchConcurrency, additionalTopicTokenMap, cancellationToken)).ConfigureAwait(false);
            }

            public async Task StopAsync(CancellationToken cancellationToken = default)
            {
                await Task.WhenAll(
                    this.createDiscoveredAssetEndpointProfileCommandExecutor.StopAsync(cancellationToken),
                    this.createDiscoveredAssetCommandExecutor.StopAsync(cancellationToken)).ConfigureAwait(false);
            }
            private async Task<ExtendedResponse<CreateDiscoveredAssetEndpointProfileResponsePayload>> CreateDiscoveredAssetEndpointProfileInt(ExtendedRequest<CreateDiscoveredAssetEndpointProfileRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<CreateDiscoveredAssetEndpointProfileResponsePayload> extended = await this.CreateDiscoveredAssetEndpointProfileAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<CreateDiscoveredAssetEndpointProfileResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }
            private async Task<ExtendedResponse<CreateDiscoveredAssetResponsePayload>> CreateDiscoveredAssetInt(ExtendedRequest<CreateDiscoveredAssetRequestPayload> req, CancellationToken cancellationToken)
            {
                ExtendedResponse<CreateDiscoveredAssetResponsePayload> extended = await this.CreateDiscoveredAssetAsync(req.Request!, req.RequestMetadata!, cancellationToken);
                return new ExtendedResponse<CreateDiscoveredAssetResponsePayload> { Response = extended.Response, ResponseMetadata = extended.ResponseMetadata };
            }

            public async ValueTask DisposeAsync()
            {
                await this.createDiscoveredAssetEndpointProfileCommandExecutor.DisposeAsync().ConfigureAwait(false);
                await this.createDiscoveredAssetCommandExecutor.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.createDiscoveredAssetEndpointProfileCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
                await this.createDiscoveredAssetCommandExecutor.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }

        public abstract partial class Client : IAsyncDisposable
        {
            private ApplicationContext applicationContext;
            private IMqttPubSubClient mqttClient;
            private readonly CreateDiscoveredAssetEndpointProfileCommandInvoker createDiscoveredAssetEndpointProfileCommandInvoker;
            private readonly CreateDiscoveredAssetCommandInvoker createDiscoveredAssetCommandInvoker;

            public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
            {
                this.applicationContext = applicationContext;
                this.mqttClient = mqttClient;

                this.createDiscoveredAssetEndpointProfileCommandInvoker = new CreateDiscoveredAssetEndpointProfileCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.createDiscoveredAssetEndpointProfileCommandInvoker.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
                this.createDiscoveredAssetCommandInvoker = new CreateDiscoveredAssetCommandInvoker(applicationContext, mqttClient);
                if (topicTokenMap != null)
                {
                    foreach (string topicTokenKey in topicTokenMap.Keys)
                    {
                        this.createDiscoveredAssetCommandInvoker.TopicTokenMap.TryAdd(topicTokenKey, topicTokenMap[topicTokenKey]);
                    }
                }
            }

            public CreateDiscoveredAssetEndpointProfileCommandInvoker CreateDiscoveredAssetEndpointProfileCommandInvoker { get => this.createDiscoveredAssetEndpointProfileCommandInvoker; }
            public CreateDiscoveredAssetCommandInvoker CreateDiscoveredAssetCommandInvoker { get => this.createDiscoveredAssetCommandInvoker; }


            public RpcCallAsync<CreateDiscoveredAssetEndpointProfileResponsePayload> CreateDiscoveredAssetEndpointProfileAsync(CreateDiscoveredAssetEndpointProfileRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                additionalTopicTokenMap["invokerClientId"] = clientId;

                return new RpcCallAsync<CreateDiscoveredAssetEndpointProfileResponsePayload>(this.createDiscoveredAssetEndpointProfileCommandInvoker.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public RpcCallAsync<CreateDiscoveredAssetResponsePayload> CreateDiscoveredAssetAsync(CreateDiscoveredAssetRequestPayload request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
            {
                string? clientId = this.mqttClient.ClientId;
                if (string.IsNullOrEmpty(clientId))
                {
                    throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
                }

                CommandRequestMetadata metadata = requestMetadata ?? new CommandRequestMetadata();
                additionalTopicTokenMap ??= new();

                additionalTopicTokenMap["invokerClientId"] = clientId;

                return new RpcCallAsync<CreateDiscoveredAssetResponsePayload>(this.createDiscoveredAssetCommandInvoker.InvokeCommandAsync(request, metadata, additionalTopicTokenMap, commandTimeout, cancellationToken), metadata.CorrelationId);
            }

            public async ValueTask DisposeAsync()
            {
                await this.createDiscoveredAssetEndpointProfileCommandInvoker.DisposeAsync().ConfigureAwait(false);
                await this.createDiscoveredAssetCommandInvoker.DisposeAsync().ConfigureAwait(false);
            }

            public async ValueTask DisposeAsync(bool disposing)
            {
                await this.createDiscoveredAssetEndpointProfileCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
                await this.createDiscoveredAssetCommandInvoker.DisposeAsync(disposing).ConfigureAwait(false);
            }
        }
    }
}
