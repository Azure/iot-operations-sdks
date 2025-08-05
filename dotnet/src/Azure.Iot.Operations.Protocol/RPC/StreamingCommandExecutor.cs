// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Events;
#pragma warning disable IDE0060 // Remove unused parameter

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

        private async Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            if (IsNewStreamingCommand())
            {
                CancellationTokenSource cts = new();
                var cancellationTokenRegistration = cts.Token.Register(async () =>
                {
                    await CancelStreamingCommandAsync(new Guid("TODO"));
                });

                IAsyncEnumerable<StreamingExtendedResponse<TResp>> responseStream = OnStreamingCommandReceived(GetMockRequestStream(cts.Token), cts.Token);

                await foreach (StreamingExtendedResponse<TResp> response in responseStream.WithCancellation(cts.Token))
                {
                    // Publish MQTT message with response
                }

                // No need to send cancellation message to invoker once all responses have been streamed
                cancellationTokenRegistration.Unregister();
            }
            else if (IsRequestInExistingStream())
            {
                // Feed an existing IAsyncEnumerable unless it has been cancelled
            }
        }

        public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
        {
            new Task(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10));
                IAsyncEnumerable<StreamingExtendedResponse<TResp>> responseStream = OnStreamingCommandReceived.Invoke(GetMockRequestStream(), new CancellationTokenSource().Token);
            }).Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CancelStreamingCommandAsync(Guid correlationId)
        {
            return Task.CompletedTask;
        }

        private static async IAsyncEnumerable<StreamingExtendedRequest<TReq>> GetMockRequestStream([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
                yield return new StreamingExtendedRequest<TReq>();
            }
        }

        private bool IsNewStreamingCommand()
        {
            throw new NotImplementedException();
        }

        private bool IsRequestInExistingStream()
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
#pragma warning restore IDE0060 // Remove unused parameter
