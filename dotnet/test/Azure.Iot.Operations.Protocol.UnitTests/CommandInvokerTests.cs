// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        public async Task InvokeOneRequestIllegalTimeout()
        {
            var mock = new MockMqttPubSubClient("mockClient");
            await using var invoker = new InvokerStub(mock)
            {
                RequestTopicPattern = "command/{executorId}/mockCommand",
            };

            var invokeTask = invoker.InvokeCommandAsync("req Payload", null, commandTimeout: TimeSpan.FromSeconds(-1));

            var ex = await Assert.ThrowsAsync<AkriMqttException>(() => invokeTask);
            Assert.Equal(AkriMqttErrorKind.ArgumentInvalid, ex.Kind);
            Assert.False(ex.InApplication);
            Assert.True(ex.IsShallow);
            Assert.False(ex.IsRemote);
            Assert.Null(ex.HttpStatusCode);
            Assert.Equal("commandTimeout", ex.PropertyName);
            Assert.Equal(TimeSpan.FromSeconds(-1), ex.PropertyValue);
            Assert.Null(ex.CorrelationId);
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