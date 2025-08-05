// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Events;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private readonly int[] _supportedMajorProtocolVersions = [CommandVersion.MajorProtocolVersion];

        internal static IWallClock WallClock = new WallClock();

        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;

        private readonly IPayloadSerializer _serializer;

        private readonly ApplicationContext _applicationContext;

        /// <summary>
        /// The timeout for all commands received by this executor.
        /// </summary>
        /// <remarks>
        /// Note that a command invoker may also send a per-invocation timeout. When this happens, a command will timeout if it exceeds either
        /// of these timeout values.
        /// </remarks>
        public TimeSpan ExecutionTimeout { get; set; }

        /// <summary>
        /// A streaming command was invoked
        /// </summary>
        /// <remarks>
        /// The callback provides the stream of requests and requires the user to return one to many responses.
        /// </remarks>
        public required Func<IAsyncEnumerable<StreamingExtendedRequest<TReq>>, CancellationToken, Task<IAsyncEnumerable<StreamingExtendedResponse<TResp>>>> OnStreamingCommandReceived { get; set; }

        public string? ExecutorId { get; init; }

        public string ServiceGroupId { get; init; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The topic token replacement map that this executor will use by default. Generally, this will include the token values
        /// for topic tokens such as "executorId" which should be the same for the duration of this command executor's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified when starting the executor by specifying the additionalTopicToken map in <see cref="StartAsync(int?, Dictionary{string, string}?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public StreamingCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }
            _applicationContext = applicationContext;
            _mqttClient = mqttClient ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(mqttClient), string.Empty);
            _commandName = commandName;
            _serializer = serializer ?? throw AkriMqttException.GetArgumentInvalidException(commandName, nameof(serializer), string.Empty);

            ServiceGroupId = AttributeRetriever.GetAttribute<ServiceGroupIdAttribute>(this)?.Id ?? string.Empty;
            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;

            mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
            TopicTokenMap = new();
        }

        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task CancelStreamingCommandAsync(Guid correlationId)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
