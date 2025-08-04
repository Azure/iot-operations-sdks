// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Events;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public abstract class StreamingCommandInvoker<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        private readonly int[] _supportedMajorProtocolVersions = [CommandVersion.MajorProtocolVersion];

        private readonly IMqttPubSubClient _mqttClient;
        private readonly string _commandName;
        private readonly IPayloadSerializer _serializer;

        private readonly ApplicationContext _applicationContext;

        /// <summary>
        /// The topic token replacement map that this command invoker will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this command invoker's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified per-method invocation by specifying the additionalTopicToken map in <see cref="InvokeCommandAsync(TReq, CommandRequestMetadata?, Dictionary{string, string}?, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The prefix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no prefix or suffix is specified, and no value is provided in <see cref="ResponseTopicPattern"/>, then this
        /// value will default to "clients/{invokerClientId}" for security purposes.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicPrefix { get; set; }

        /// <summary>
        /// The suffix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no suffix is specified, then the command response topic won't include a suffix.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicSuffix { get; set; }

        /// <summary>
        /// If provided, this topic pattern will be used for command response topic.
        /// </summary>
        /// <remarks>
        /// If not provided, and no value is provided for <see cref="ResponseTopicPrefix"/> or <see cref="ResponseTopicSuffix"/>, the default pattern used will be clients/{mqtt client id}/{request topic pattern}.
        /// </remarks>
        public string? ResponseTopicPattern { get; set; }

        public StreamingCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer)
        {
            _applicationContext = applicationContext;
            if (commandName == null || commandName == string.Empty)
            {
                throw AkriMqttException.GetConfigurationInvalidException(nameof(commandName), string.Empty);
            }

            _mqttClient = mqttClient ?? throw AkriMqttException.GetConfigurationInvalidException(commandName, nameof(mqttClient), string.Empty);
            _commandName = commandName;
            _serializer = serializer ?? throw AkriMqttException.GetConfigurationInvalidException(commandName, nameof(serializer), string.Empty);

            RequestTopicPattern = AttributeRetriever.GetAttribute<CommandTopicAttribute>(this)?.RequestTopic ?? string.Empty;

            _mqttClient.ApplicationMessageReceivedAsync += MessageReceivedCallbackAsync;
            TopicTokenMap = new();
        }

        public IAsyncEnumerable<StreamingExtendedResponse<TResp>> InvokeStreamingCommandAsync(IAsyncEnumerable<TReq> requests, CommandRequestMetadata? metadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task CancelStreamingCommandAsync(Guid correlationId)
        {
            throw new NotImplementedException();
        }

        private Task MessageReceivedCallbackAsync(MqttApplicationMessageReceivedEventArgs args)
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
