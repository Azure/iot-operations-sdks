using System.Text;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Azure.Iot.Operations.Protocol.UnitTests
{

    class InvokerStub : CommandInvoker<string, string>
    {
        public InvokerStub(IMqttPubSubClient mqttClient, string? commandName = "myCmd") : base(mqttClient, commandName!, new Utf8JsonSerializer()) { }
    }

    class InvokerStubProtobuf : CommandInvoker<string, string>
    {
        public InvokerStubProtobuf(IMqttPubSubClient mqttClient) : base(mqttClient, "myCmd", new ProtobufSerializer<Empty, Empty>()) { }
    }

    public class CommandInvokerTests
    {
        [Fact]
        public void ConstructInvokerWithNullNameThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            var exception = Assert.Throws<AkriMqttException>(() => { new InvokerStub(mock, null); });
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public void ConstructInvokerWithEmptyNameThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            var exception = Assert.Throws<AkriMqttException>(() => { new InvokerStub(mock, string.Empty); });
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Equal(string.Empty, exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task MqttProtocolVersionUnknownThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient", MqttProtocolVersion.Unknown);
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.Unknown, exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task MqttProtocolVersion310ThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient", MqttProtocolVersion.V310);
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.V310, exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task MqttProtocolVersion311ThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient", MqttProtocolVersion.V311);
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("MQTTClient.ProtocolVersion", exception.PropertyName);
            Assert.Equal(MqttProtocolVersion.V311, exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task InvalidRequestTopicPatternThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/{unknown}/stub",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("unknown", exception.PropertyName);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task RequestTopicModelIdWithoutReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/{modelId}/echo",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("modelId", exception.PropertyName);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task RequestTopicModelIdWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/{modelId}/echo",
            };

            stub.TopicTokenMap["modelId"] = "Invalid//Model";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("modelId", exception.PropertyName);
            Assert.Equal("Invalid//Model", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task RequestTopicCommandNameWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock, "invalid//name")
            {
                RequestTopicPattern = "mock/{commandName}/echo",
            };

            stub.TopicTokenMap["commandName"] = "invalid//name";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Equal("invalid//name", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task RequestWithInvalidMetadataThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
            };

            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["__hasReservePrefix"] = "userValue";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request", requestMetadata));
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("metadata", exception.PropertyName);
        }

        [Fact]
        public async Task InvalidTopicNamespaceThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
                TopicNamespace = "invalid/{modelId}",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("TopicNamespace", exception.PropertyName);
            Assert.Equal("invalid/{modelId}", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task InvalidResponseTopicPrefixThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicPrefix = "invalid//{unknown}",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("ResponseTopicPrefix", exception.PropertyName);
            Assert.Equal("invalid//{unknown}", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task ResponseTopicPrefixModelIdWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicPrefix = "valid/{modelId}",
            };

            stub.TopicTokenMap["modelId"] = "Invalid//Model";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("modelId", exception.PropertyName);
            Assert.Equal("Invalid//Model", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task ResponseTopicPrefixCommandNameWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock, "invalid//name")
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicPrefix = "valid/{commandName}",
            };

            stub.TopicTokenMap["commandName"] = "invalid//name";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Equal("invalid//name", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task InvalidResponseTopicSuffixThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock, "invalid//name")
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicSuffix = "invalid//{unknown}",
            };

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("ResponseTopicSuffix", exception.PropertyName);
            Assert.Equal("invalid//{unknown}", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task ResponseTopicSuffixModelIdWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicSuffix = "valid/{modelId}"
            };

            stub.TopicTokenMap["modelId"] = "Invalid//Model";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("modelId", exception.PropertyName);
            Assert.Equal("Invalid//Model", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task ResponseTopicSuffixCommandNameWithInvalidReplacementThrowsException()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock, "invalid//name")
            {
                RequestTopicPattern = "mock/stub",
                ResponseTopicSuffix = "valid/{commandName}"
            };

            stub.TopicTokenMap["commandName"] = "invalid//name";

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("commandName", exception.PropertyName);
            Assert.Equal("invalid//name", exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task InvokerFailsIfTopicNotConfigured()
        {
            MockMqttPubSubClient mock = new();
            InvokerStub stub = new(mock);

            var exception = await Assert.ThrowsAsync<AkriMqttException>(() => stub.InvokeCommandAsync("request"));
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, exception.Kind);
            Assert.False(exception.InApplication);
            Assert.True(exception.IsShallow);
            Assert.False(exception.IsRemote);
            Assert.Null(exception.HttpStatusCode);
            Assert.Equal("RequestTopicPattern", exception.PropertyName);
            Assert.Equal(string.Empty, exception.PropertyValue);
            Assert.Null(exception.CorrelationId);
        }

        [Fact]
        public async Task InvokeWithExecutorId()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = stub.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "myVirtualService" } });
            Assert.Equal("clients/mockClient/command/+/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/myVirtualService/mockCommand", mock.MessagePublished.Topic);
            Assert.Equal(MqttPayloadFormatIndicator.CharacterData, mock.MessagePublished.PayloadFormatIndicator);

            var responseMsg = new MqttApplicationMessage("clients/mockClient/command/myVirtualService/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.Equal(MqttPayloadFormatIndicator.CharacterData, mock.MessagePublished.PayloadFormatIndicator);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata.CorrelationId);
        }

        [Fact]
        public async Task InvokerWithCustomPrefix()
        {
            const string namespacePrefix = "country/city/factory/floor/ovens";
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "{executorId}/commands/{commandName}",
                TopicNamespace = namespacePrefix,
                ResponseTopicPrefix = null,
                ResponseTopicSuffix = "_for/{invokerClientId}",
            };

            stub.TopicTokenMap["commandName"] = "myCmd";
            stub.TopicTokenMap["invokerClientId"] = "mockClient";

            var invokeTask = stub.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "stubServer" } });
            Assert.Equal(namespacePrefix + "/+/commands/myCmd/_for/mockClient", mock.SubscribedTopicReceived);
            Assert.Equal(namespacePrefix + "/stubServer/commands/myCmd", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage(namespacePrefix + "/stubServer/commands/myCmd/_for/mockClient")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task InvokerWithCustomTopicToken()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "{executorId}/commands/{commandName}/{ex:foobar}",
                ResponseTopicPrefix = null,
                ResponseTopicSuffix = "_for/{invokerClientId}",
            };

            stub.TopicTokenMap["commandName"] = "myCmd";
            stub.TopicTokenMap["invokerClientId"] = "mockClient";
            stub.TopicTokenMap["ex:foobar"] = "MyValue";

            var invokeTask = stub.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "stubServer" } });
            Assert.Equal("+/commands/myCmd/MyValue/_for/mockClient", mock.SubscribedTopicReceived);
            Assert.Equal("stubServer/commands/myCmd/MyValue", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage("stubServer/commands/myCmd/MyValue/_for/mockClient")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task InvokeWithExecutorIdWithCustomResponse()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/someExecutor/mockCommand",
                GetResponseTopic = (string _) => { return "my/uns/prefix/command/someExecutor/mockCommand"; },
            };

            var invokeTask = stub.InvokeCommandAsync("req Payload");
            Assert.Equal("my/uns/prefix/command/someExecutor/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage("my/uns/prefix/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");
            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task InvokeWithExecutorIdAndCustomResponse()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "not-uns/prefix/{invokerClientId}"
            };

            stub.TopicTokenMap["invokerClientId"] = "mockClient";

            var invokeTask = stub.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "stubServer" } });
            Assert.Equal("not-uns/prefix/mockClient/command/+/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/stubServer/mockCommand", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage("not-uns/prefix/mockClient/command/stubServer/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task InvokeWithTwoExecutorId()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/{executorId}/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = stub.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "stubServer" } });
            Assert.Equal("clients/mockClient/command/+/+/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/stubServer/stubServer/mockCommand", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage("clients/mockClient/command/stubServer/stubServer/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task InvokeWithoutExecutorId()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = stub.InvokeCommandAsync("req Payload");
            Assert.Equal("clients/mockClient/command/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/mockCommand", mock.MessagePublished.Topic);

            var responseMsg = new MqttApplicationMessage("clients/mockClient/command/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);

            ExtendedResponse<string> response = await invokeTask;
            Assert.Equal("hola", response.Response);
            Assert.NotNull(response.ResponseMetadata?.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
        }

        [Fact]
        public async Task TwoEquivalentCallsWithinTimeoutWithoutExecutorId()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            Task<ExtendedResponse<string>> invokeTask1 = stub.InvokeCommandAsync("req Payload");
            Assert.Equal("clients/mockClient/command/mockCommand", mock.SubscribedTopicReceived);
            Assert.Equal("command/mockCommand", mock.MessagePublished.Topic);
            Assert.NotNull(mock.MessagePublished.CorrelationData);
            byte[] cid1 = mock.MessagePublished.CorrelationData;

            var responseMsg = new MqttApplicationMessage("clients/mockClient/command/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = cid1,
            };

            responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

            Task<ExtendedResponse<string>> invokeTask2 = stub.InvokeCommandAsync("req Payload");
            Assert.Equal("command/mockCommand", mock.MessagePublished.Topic);
            Assert.NotNull(mock.MessagePublished.CorrelationData);
            byte[] cid2 = mock.MessagePublished.CorrelationData;

            var responseMsg2 = new MqttApplicationMessage("clients/mockClient/command/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"hola\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = cid2,
            };

            responseMsg2.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(responseMsg);
            await mock.SimulateNewMessage(responseMsg2);

            var response1 = await invokeTask1;
            var response2 = await invokeTask2;

            Assert.Equal("hola", response1.Response);
            Assert.NotNull(response1.ResponseMetadata?.CorrelationId);
            Assert.Equal(cid1, response1.ResponseMetadata.CorrelationId.Value.ToByteArray());

            Assert.Equal("hola", response2.Response);
            Assert.NotNull(response2.ResponseMetadata?.CorrelationId);
            Assert.Equal(cid2, response2.ResponseMetadata.CorrelationId.Value.ToByteArray());
        }

        [Fact]
        public async Task InvokeDifferentCallsOutsideTimeoutWithoutExecutorId()
        {
            MockMqttPubSubClient mock = new("mockClient");
            InvokerStub stub = new(mock)
            {
                RequestTopicPattern = "command/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            for (int i = 0; i < 2; i++)
            {
                var invokeTask = stub.InvokeCommandAsync($"req Payload {i}");
                Assert.Equal("clients/mockClient/command/mockCommand", mock.SubscribedTopicReceived);
                Assert.Equal("command/mockCommand", mock.MessagePublished.Topic);

                var responseMsg = new MqttApplicationMessage("clients/mockClient/command/mockCommand")
                {
                    PayloadSegment = Encoding.UTF8.GetBytes($"\"hola {i}\""),
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                    CorrelationData = mock.MessagePublished.CorrelationData,
                };

                responseMsg.AddUserProperty(AkriSystemProperties.Status, "200");

                await mock.SimulateNewMessage(responseMsg);

                var response = await invokeTask;
                Assert.Equal($"hola {i}", response.Response);
                Assert.NotNull(response.ResponseMetadata?.CorrelationId);
                Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), response.ResponseMetadata?.CorrelationId);
            }
        }

        [Fact]
        public async Task TwoCallsWithExecutorId()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            for (int i = 0; i < 2; i++)
            {
                var invokeTask = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
                Assert.Equal("clients/mockClient/command/+/mockCommand", mock.SubscribedTopicReceived);
                Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

                var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
                {
                    PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                    CorrelationData = mock.MessagePublished.CorrelationData,
                };

                response.AddUserProperty(AkriSystemProperties.Status, "200");

                await mock.SimulateNewMessage(response);

                var invokerResponse = await invokeTask;
                Assert.Equal("testPayload", invokerResponse.Response);
            }
        }

        [Theory]
        [InlineData(2, 10)]
        [InlineData(5, 50)]
        [InlineData(10, 100)]
        public async Task InvokeConcurrentLegalRequests(int maxConcurrentRequests, int totalRequests)
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // Complete maxConcurrentRequests concurrent requests totalRequests times in a row
            for (int i = 0; i < totalRequests; i += maxConcurrentRequests)
            {
                var tasks = new Task[maxConcurrentRequests];
                for (int j = 0; j < maxConcurrentRequests; j++)
                {
                    var task = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", $"executor{j}" } });
                    Assert.Equal("clients/mockClient/command/+/mockCommand", mock.SubscribedTopicReceived);
                    Assert.Equal($"command/executor{j}/mockCommand", mock.MessagePublished.Topic);

                    tasks[j] = task;

                    var response = new MqttApplicationMessage($"clients/mockClient/command/executor{j}/mockCommand")
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes($"\"testPayload{j}\""),
                        PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                        CorrelationData = mock.MessagePublished.CorrelationData,
                    };

                    response.AddUserProperty(AkriSystemProperties.Status, "200");

                    await mock.SimulateNewMessage(response);
                    var invokerResponse = await task;

                    Assert.Equal($"testPayload{j}", invokerResponse.Response);
                }
                await Task.WhenAll(tasks);
            }
        }

        [Theory]
        [InlineData(2, 4)]
        [InlineData(5, 50)]
        [InlineData(10, 100)]
        public async Task InvokeConcurrentRequestsWithFreshness(int maxConcurrentRequests, int totalRequests)
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // Complete maxConcurrentRequests concurrent requests totalRequests times in a row
            for (int i = 0; i < totalRequests; i += maxConcurrentRequests)
            {
                var tasks = new Task[maxConcurrentRequests];
                for (int j = 0; j < maxConcurrentRequests; j++)
                {
                    var task = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
                    Assert.Equal("clients/mockClient/command/+/mockCommand", mock.SubscribedTopicReceived);
                    Assert.Equal($"command/someExecutor/mockCommand", mock.MessagePublished.Topic);

                    tasks[j] = task;

                    var response = new MqttApplicationMessage($"clients/mockClient/command/someExecutor/mockCommand")
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes($"\"resp Payload\""),
                        PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                        CorrelationData = mock.MessagePublished.CorrelationData,
                    };

                    response.AddUserProperty(AkriSystemProperties.Status, "200");

                    await mock.SimulateNewMessage(response);
                    var invokerResponse = await task;

                    Assert.Equal($"resp Payload", invokerResponse.Response);
                }
                await Task.WhenAll(tasks);
            }
        }

        [Fact]
        public async Task InvokerRequestNoStatusProperty()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = invoker.InvokeCommandAsync("malformed?", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            await mock.SimulateNewMessage(response);
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.HeaderMissing, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokerRequestBadPayload()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = invoker.InvokeCommandAsync("malformed?", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "400");

            await mock.SimulateNewMessage(response);
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.PayloadInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.True(ex.IsRemote);
            Assert.Equal((int)CommandStatusCode.BadRequest, ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeLegalRequestWrongParameterType()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // String encoded version of invalid payload type
            var invokeTask = invoker.InvokeCommandAsync("12", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "422");
            response.AddUserProperty(AkriSystemProperties.IsApplicationError, "true");

            await mock.SimulateNewMessage(response);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.InvocationException, ex.Kind);
            Assert.True(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.True(ex.IsRemote);
            Assert.Equal((int)CommandStatusCode.UnprocessableContent, ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeLegalRequestWrongContentTypeInvokerSide()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStubProtobuf(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            var invokeTask = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                ContentType = "application/json",
                PayloadSegment = Encoding.UTF8.GetBytes("\"\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };
            response.AddUserProperty(AkriSystemProperties.Status, "200");
            response.AddUserProperty(AkriSystemProperties.InvalidPropertyName, "ContentType");
            response.AddUserProperty(AkriSystemProperties.InvalidPropertyValue, "application/json");

            await mock.SimulateNewMessage(response);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.HeaderInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("Content Type", ex.HeaderName);
            Assert.Equal("application/json", ex.HeaderValue);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeLegalRequestInvokerDeserializationError()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // String encoded version of invalid payload type
            var invokeTask = invoker.InvokeCommandAsync("12", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            // Deserialization of executor response throws on invoker side
            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("invalid\\\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(response);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.PayloadInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
        }

        [Fact]
        public async Task InvokeOneRequestIllegalTimeout()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            var invokeTask = invoker.InvokeCommandAsync("req Payload", null, commandTimeout: TimeSpan.FromSeconds(-1));

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.ConfigurationInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("commandTimeout", ex.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndTimeOut()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            var invokeTask = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndDrop()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            var invokeTask = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(1));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic); ;

            // No response, connection "dropped"
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndDropPubAck()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            // Default timeout
            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_dropPubAck"] = "true";

            var invokeTask = invoker.InvokeCommandAsync(
                "req Payload",
                requestMetadata,
                transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } },
                commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            // Puback dropped, keeps on retrying until timeout
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("myCmd", ex.CommandName);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndFailurePubAck()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_failPubAck"] = "true";
            var invokeTask = invoker.InvokeCommandAsync("req Payload", requestMetadata, transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndPubAckNotAuthorized()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_failPubAck"] = "true";
            requestMetadata.UserData["_notAuthorized"] = "true";
            var invokeTask = invoker.InvokeCommandAsync("req Payload", requestMetadata, transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("myCmd", ex.CommandName);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Fact]
        public async Task InvokeOneRequestAndPubAckNoMatchingSubscribers()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_failPubAck"] = "true";
            requestMetadata.UserData["_noMatchingSubscribers"] = "true";
            var invokeTask = invoker.InvokeCommandAsync("req Payload", requestMetadata, transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("myCmd", ex.CommandName);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task InvokerSequenceSameRequests(int numberOfRequests)
        {
            int numberOfResponses = 0;
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                // Each request is different
                RequestTopicPattern = string.Concat("command/{executorId}/mockCommand"),
                ResponseTopicPrefix = "clients/mockClient",
            };

            for (int i = 0; i < numberOfRequests; i++)
            {
                var invokeTask = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
                Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

                var response = new MqttApplicationMessage($"clients/mockClient/command/someExecutor/mockCommand")
                {
                    PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                    CorrelationData = mock.MessagePublished.CorrelationData,
                };

                response.AddUserProperty(AkriSystemProperties.Status, "200");

                await mock.SimulateNewMessage(response);

                var invokerResponse = await invokeTask;
                Assert.Equal("testPayload", invokerResponse.Response);
                numberOfResponses++;
            }
            Assert.Equal(numberOfRequests, numberOfResponses);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task InvokerSequenceDifferentRequest(int numberOfRequests)
        {
            int numberOfResponses = 0;
            List<ExtendedResponse<string>> responses = new();

            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = string.Concat("command/{executorId}/mockCommand"),
                ResponseTopicPrefix = "clients/mockClient",
            };

            for (int i = 0; i < numberOfRequests; i++)
            {
                // Each request is different
                var invokeTask = invoker.InvokeCommandAsync($"req Payload{i}", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
                Assert.Equal($"command/someExecutor/mockCommand", mock.MessagePublished.Topic);

                var response = new MqttApplicationMessage($"clients/mockClient/command/someExecutor/mockCommand")
                {
                    PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                    CorrelationData = mock.MessagePublished.CorrelationData,
                };

                response.AddUserProperty(AkriSystemProperties.Status, "200");

                await mock.SimulateNewMessage(response);

                var invokerResponse = await invokeTask;
                Assert.Equal("testPayload", invokerResponse.Response);

                // Keep track of each different response
                if (!responses.Contains(invokerResponse))
                {
                    responses.Add(invokerResponse);
                }
                else
                {
                    throw new Exception("Same response recieved");
                }

                numberOfResponses++;
            }
            Assert.Equal(numberOfRequests, numberOfResponses);
            Assert.Equal(responses.Count, numberOfResponses);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task InvokerSequenceFirstRequestTimesOut(int numberOfRequests)
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = string.Concat("command/{executorId}/mockCommand"),
                ResponseTopicPrefix = "clients/mockClient",
            };

            List<ExtendedResponse<string>> responses = new();

            for (int i = 0; i < numberOfRequests; i++)
            {
                var invokeTask = invoker.InvokeCommandAsync($"req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
                Assert.Equal($"command/someExecutor/mockCommand", mock.MessagePublished.Topic);

                if (i == 0)
                {
                    // First request times out
                    var response = new MqttApplicationMessage($"clients/mockClient/command/someExecutor/timeOut")
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                        PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                        CorrelationData = mock.MessagePublished.CorrelationData,
                    };

                    response.AddUserProperty(AkriSystemProperties.Status, "200");

                    await mock.SimulateNewMessage(response);

                    var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
                    Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
                    Assert.False(ex.InApplication);
                    Assert.False(ex.IsShallow);
                    Assert.False(ex.IsRemote);
                    Assert.Null(ex.HttpStatusCode);
                    Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);
                }
                else
                {
                    // Subsequent requests are sent successfully
                    var response = new MqttApplicationMessage($"clients/mockClient/command/someExecutor/mockCommand")
                    {
                        PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                        PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                        CorrelationData = mock.MessagePublished.CorrelationData,
                    };

                    response.AddUserProperty(AkriSystemProperties.Status, "200");

                    await mock.SimulateNewMessage(response);

                    var invokerResponse = await invokeTask;
                    Assert.Equal("testPayload", invokerResponse.Response);
                    responses.Add(invokerResponse);
                }
            }
            Assert.Equal(numberOfRequests - 1, responses.Count);
        }

        [Fact]
        public async Task InvokerDisconnectsBeforePuback()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // Default timeout
            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_dropPubAck"] = "true";
            var invokeRequest = invoker.InvokeCommandAsync(
                "req Payload",
                requestMetadata,
                transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } },
                commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            // Puback dropped, invoker disconnects
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeRequest);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("myCmd", ex.CommandName);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);

            // Invoker reconnects and receives response
            invokeRequest = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(response);

            var invokerResponse = await invokeRequest;
            Assert.Equal("testPayload", invokerResponse.Response);
        }

        [Fact]
        public async Task InvokerDisconnectsAfterPuback()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            // Default timeout
            var invokeRequest = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            // Puback accepted but invoker disconnects before receiving a response
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeRequest);
            Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);

            // Invoker reconnects and receives response
            invokeRequest = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });

            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(response);

            var invokerResponse = await invokeRequest;
            Assert.Equal("testPayload", invokerResponse.Response);
        }

        [Fact]
        public async Task InvokerTimesOutAndResendsNewRequest()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
                ResponseTopicPrefix = "clients/mockClient",
            };

            CommandRequestMetadata requestMetadata = new();
            requestMetadata.UserData["_dropPubAck"] = "true";

            var firstInvoke = invoker.InvokeCommandAsync("req Payload", requestMetadata, transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, commandTimeout: TimeSpan.FromSeconds(3));
            Assert.Equal("command/someExecutor/mockCommand", mock.MessagePublished.Topic);

            // Puback dropped, invoker disconnects
            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => firstInvoke);
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal(new Guid(mock.MessagePublished.CorrelationData!), ex.CorrelationId);

            var secondInvoke = invoker.InvokeCommandAsync("req Payload", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } });
            // Invoker reconnects and receives response
            var response = new MqttApplicationMessage("clients/mockClient/command/someExecutor/mockCommand")
            {
                PayloadSegment = Encoding.UTF8.GetBytes("\"testPayload\""),
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                CorrelationData = mock.MessagePublished.CorrelationData,
            };

            response.AddUserProperty(AkriSystemProperties.Status, "200");

            await mock.SimulateNewMessage(response);

            var invokerResponse = await secondInvoke;
            Assert.Equal("testPayload", invokerResponse.Response);
        }

        [Fact]
        public async Task InvokerSubscribeThrowException()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            // specify the subAck return code in the response topic string
            string responseTopic = "clients/mockClient/command/someExecutor/mockCommand/subAckUnspecifiedError";
            var ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await invoker.SubscribeAsNeededAsync(responseTopic));
            Assert.Equal(AkriMqttErrorKind.MqttError, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.False(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("myCmd", ex.CommandName);

            string expectedExMessage = $"Failed to subscribe to topic '{responseTopic}' because {MqttClientSubscribeReasonCode.UnspecifiedError}.";
            Assert.Equal(expectedExMessage, ex.Message);
        }

        [Fact]
        public async Task InvokerThrowsIfAccessedWhenDisposed()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            await invoker.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => invoker.InvokeCommandAsync("someRequest", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }));
        }

        [Fact]
        public async Task InvokerThrowsIfCancellationRequested()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => invoker.InvokeCommandAsync("someRequest", transientTopicTokenMap: new Dictionary<string, string> { { "executorId", "someExecutor" } }, cancellationToken: cts.Token));
        }
    }
}