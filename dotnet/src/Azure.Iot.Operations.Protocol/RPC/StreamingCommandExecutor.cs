// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        public required Func<IAsyncEnumerable<StreamingExtendedRequest<TReq>>, CancellationToken, IAsyncEnumerable<StreamingExtendedResponse<TResp>>> OnStreamingCommandReceived { get; set; }

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

#pragma warning disable IDE0060 // Remove unused parameter
        public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            new Task(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
                IAsyncEnumerable<StreamingExtendedResponse<TResp>> responseStream = OnStreamingCommandReceived.Invoke(GetRequestStream(), new CancellationTokenSource().Token);
            }).Start();
            return Task.CompletedTask;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public Task StopAsync(CancellationToken cancellationToken = default)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.CompletedTask;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public Task CancelStreamingCommandAsync(Guid correlationId)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.CompletedTask;
        }

        private static async IAsyncEnumerable<StreamingExtendedRequest<TReq>> GetRequestStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
                yield return new StreamingExtendedRequest<TReq>();
            }
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
