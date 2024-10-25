﻿using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Tomlyn;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TestModel.dtmi_test_TestModel__1;
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Converters;

namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class CommandExecutorTester
    {
        private const string testCasesPath = "../../../../../../eng/test/test-cases";
        private const string executorCasesPath = $"{testCasesPath}/Protocol/CommandExecutor";
        private const string defaultsFileName = "defaults.toml";

        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly HashSet<string> problematicTestCases = new HashSet<string>
        {
            "CommandExecutorReceivesPseudoDuplicateIdempotentRequest_CommandExecuted",
            "CommandExecutorReceivesPseudoDuplicateNonIdempotentRequest_CommandExecuted",
        };

        private static IDeserializer yamlDeserializer;
        private static AsyncAtomicInt TestCaseIndex = new(0);
        private static FreezableWallClock freezableWallClock;
        private static IPayloadSerializer payloadSerializer;
        private static ConcurrentDictionary<int, ConcurrentDictionary<string, AsyncAtomicInt>> sessionRequestResponseSequencers;
        private static ConcurrentDictionary<int, ConcurrentDictionary<string, AsyncAtomicInt>> standaloneRequestResponseSequencers;

        static CommandExecutorTester()
        {
            yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
                .WithTypeDiscriminatingNodeDeserializer(options =>
                {
                    options.AddKeyValueTypeDiscriminator<TestCaseAction>("action",
                        ("receive request", typeof(TestCaseActionReceiveRequest)),
                        ("await acknowledgement", typeof(TestCaseActionAwaitAck)),
                        ("await publish", typeof(TestCaseActionAwaitPublish)),
                        ("sync", typeof(TestCaseActionSync)),
                        ("sleep", typeof(TestCaseActionSleep)),
                        ("disconnect", typeof(TestCaseActionDisconnect)),
                        ("freeze time", typeof(TestCaseActionFreezeTime)),
                        ("unfreeze time", typeof(TestCaseActionUnfreezeTime)));
                })
                .Build();

            string defaultsFilePath = Path.Combine(executorCasesPath, defaultsFileName);
            if (File.Exists(defaultsFilePath))
            {
                DefaultTestCase defaultTestCase = Toml.ToModel<DefaultTestCase>(File.ReadAllText(defaultsFilePath), defaultsFilePath, new TomlModelOptions { ConvertPropertyName = PascalToKebabCase });

                TestCaseExecutor.DefaultCommandName = defaultTestCase.Prologue.Executor.CommandName;
                TestCaseExecutor.DefaultRequestTopic = defaultTestCase.Prologue.Executor.RequestTopic;
                TestCaseExecutor.DefaultModelId = defaultTestCase.Prologue.Executor.ModelId;
                TestCaseExecutor.DefaultExecutorId = defaultTestCase.Prologue.Executor.ExecutorId;
                TestCaseExecutor.DefaultTopicNamespace = defaultTestCase.Prologue.Executor.TopicNamespace;
                TestCaseExecutor.DefaultIdempotent = defaultTestCase.Prologue.Executor.Idempotent;
                TestCaseExecutor.DefaultCacheableDuration = defaultTestCase.Prologue.Executor.CacheableDuration;
                TestCaseExecutor.DefaultExecutorTimeout = defaultTestCase.Prologue.Executor.ExecutionTimeout;
                TestCaseExecutor.DefaultRequestResponsesMap = defaultTestCase.Prologue.Executor.RequestResponsesMap;
                TestCaseExecutor.DefaultExecutionConcurrency = defaultTestCase.Prologue.Executor.ExecutionConcurrency;

                TestCaseActionReceiveRequest.DefaultTopic = defaultTestCase.Actions.ReceiveRequest.Topic;
                TestCaseActionReceiveRequest.DefaultPayload = defaultTestCase.Actions.ReceiveRequest.Payload;
                TestCaseActionReceiveRequest.DefaultContentType = defaultTestCase.Actions.ReceiveRequest.ContentType;
                TestCaseActionReceiveRequest.DefaultFormatIndicator = defaultTestCase.Actions.ReceiveRequest.FormatIndicator;
                TestCaseActionReceiveRequest.DefaultCorrelationIndex = defaultTestCase.Actions.ReceiveRequest.CorrelationIndex;
                TestCaseActionReceiveRequest.DefaultQos = defaultTestCase.Actions.ReceiveRequest.Qos;
                TestCaseActionReceiveRequest.DefaultMessageExpiry = defaultTestCase.Actions.ReceiveRequest.MessageExpiry;
                TestCaseActionReceiveRequest.DefaultResponseTopic = defaultTestCase.Actions.ReceiveRequest.ResponseTopic;
                TestCaseActionReceiveRequest.DefaultInvokerIndex = defaultTestCase.Actions.ReceiveRequest.InvokerIndex;
            }

            freezableWallClock = new FreezableWallClock();
            TestCommandExecutor.WallClock = freezableWallClock;
            CommandResponseCache.WallClock = freezableWallClock;

            payloadSerializer = new Utf8JsonSerializer();

            sessionRequestResponseSequencers = new();
            standaloneRequestResponseSequencers = new();
        }

        public static IEnumerable<object[]> GetAllCommandExecutorCases()
        {
            foreach (string testCasePath in Directory.GetFiles(executorCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{executorCasesPath}/{testCaseName}.yaml"))
                {
                    TestCase testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
                    if (!problematicTestCases.Contains(testCaseName) &&
                        !testCase.Requires.Contains(TestFeatureKind.Unobtanium))
                    {
                        yield return new object[] { testCaseName };
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetRestrictedCommandExecutorCases()
        {
            foreach (string testCasePath in Directory.GetFiles(executorCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{executorCasesPath}/{testCaseName}.yaml"))
                {
                    Trace.TraceInformation($"Deserializing {executorCasesPath}/{testCaseName}.yaml");
                    TestCase testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
                    if (!problematicTestCases.Contains(testCaseName) &&
                        !testCase.Requires.Contains(TestFeatureKind.Unobtanium) &&
                        !testCase.Requires.Contains(TestFeatureKind.AckOrdering) &&
                        !testCase.Requires.Contains(TestFeatureKind.Reconnection))
                    {
                        yield return new object[] { testCaseName };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetRestrictedCommandExecutorCases))]
        public Task TestCommandExecutorWithSessionClient(string testCaseName)
        {
            return TestCommandExecutorProtocol(testCaseName, sessionRequestResponseSequencers, includeSessionClient: true);
        }

        [Theory]
        [MemberData(nameof(GetRestrictedCommandExecutorCases))]
        public Task TestCommandExecutorStandalone(string testCaseName)
        {
            return TestCommandExecutorProtocol(testCaseName, standaloneRequestResponseSequencers, includeSessionClient: false);
        }

        private async Task TestCommandExecutorProtocol(string testCaseName, ConcurrentDictionary<int, ConcurrentDictionary<string, AsyncAtomicInt>> requestResponseSequencers, bool includeSessionClient)
        {
            int testCaseIndex = await TestCaseIndex.Increment().ConfigureAwait(false);
            ConcurrentDictionary<string, AsyncAtomicInt> requestResponseSequencer = new();
            Assert.True(requestResponseSequencers.TryAdd(testCaseIndex, requestResponseSequencer));

            TestCase testCase;
            using (StreamReader streamReader = File.OpenText($"{executorCasesPath}/{testCaseName}.yaml"))
            {
                testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
            }

            List<TestCommandExecutor> commandExecutors = new();
            Dictionary<string, AsyncCountdownEvent> countdownEvents = new();

            string clientIdPrefix = includeSessionClient ? "Session" : "Standalone";
            string mqttClientId = testCase.Prologue?.MqttConfig?.ClientId ?? $"{clientIdPrefix}ExecutorTestClient{testCaseIndex}";
            StubMqttClient stubMqttClient = new StubMqttClient(mqttClientId);
            await using CompositeMqttClient compositeMqttClient = new CompositeMqttClient(stubMqttClient, includeSessionClient, mqttClientId);

            await compositeMqttClient.ConnectAsync().WaitAsync(TestTimeout);

            if (testCase.Prologue?.PushAcks != null)
            {
                foreach (TestAckKind ackKind in testCase.Prologue.PushAcks.Publish)
                {
                    stubMqttClient.EnqueuePubAck(ackKind);
                }

                foreach (TestAckKind ackKind in testCase.Prologue.PushAcks.Subscribe)
                {
                    stubMqttClient.EnqueueSubAck(ackKind);
                }

                foreach (TestAckKind ackKind in testCase.Prologue.PushAcks.Unsubscribe)
                {
                    stubMqttClient.EnqueueUnsubAck(ackKind);
                }
            }

            int requestCount = testCase.Actions.Count(a => a.GetType() == typeof(TestCaseActionReceiveRequest));

            foreach (KeyValuePair<string, int> kvp in testCase.Prologue?.CountdownEvents ?? new Dictionary<string, int>())
            {
                countdownEvents[kvp.Key] = new AsyncCountdownEvent(kvp.Value, requestCount + 1);
            }

            foreach (TestCaseExecutor testCaseExecutor in testCase.Prologue?.Executors ?? new List<TestCaseExecutor>())
            {
                bool isLast = ReferenceEquals(testCaseExecutor, testCase.Prologue?.Executors.Last());
                TestCommandExecutor? commandExecutor = await GetAndStartCommandExecutorAsync(compositeMqttClient, testCaseExecutor, countdownEvents, requestResponseSequencer, isLast ? testCase.Prologue?.Catch : null).ConfigureAwait(false);
                if (commandExecutor == null)
                {
                    return;
                }

                commandExecutors.Add(commandExecutor);
            }

            ConcurrentDictionary<int, Guid?> invokerIds = new();
            ConcurrentDictionary<int, string?> correlationIds = new();
            ConcurrentDictionary<int, ushort> packetIds = new();
            int freezeTicket = -1;

            try
            {
                foreach (TestCaseAction action in testCase.Actions)
                {
                    switch (action)
                    {
                        case TestCaseActionReceiveRequest actionReceiveRequest:
                            await ReceiveRequestAsync(actionReceiveRequest, stubMqttClient, invokerIds, correlationIds, packetIds, testCaseIndex).ConfigureAwait(false);
                            break;
                        case TestCaseActionAwaitAck actionAwaitAck:
                            await AwaitAcknowledgementAsync(actionAwaitAck, stubMqttClient, packetIds).ConfigureAwait(false);
                            break;
                        case TestCaseActionAwaitPublish actionAwaitPublish:
                            await AwaitPublishAsync(actionAwaitPublish, stubMqttClient, correlationIds).ConfigureAwait(false);
                            break;
                        case TestCaseActionSync actionSync:
                            await SyncEventAsync(actionSync, countdownEvents).ConfigureAwait(false);
                            break;
                        case TestCaseActionSleep actionSleep:
                            await SleepAsync(actionSleep).ConfigureAwait(false);
                            break;
                        case TestCaseActionDisconnect:
                            await DisconnectAsync(stubMqttClient).ConfigureAwait(false);
                            break;
                        case TestCaseActionFreezeTime:
                            freezeTicket = await FreezeTimeAsync().ConfigureAwait(false);
                            break;
                        case TestCaseActionUnfreezeTime:
                            await UnfreezeTimeAsync(freezeTicket).ConfigureAwait(false);
                            freezeTicket = -1;
                            break;
                    }
                }
            }
            finally
            {
                if (freezeTicket >= 0)
                {
                    await UnfreezeTimeAsync(freezeTicket).ConfigureAwait(false);
                }
            }

            if (testCase.Epilogue != null)
            {
                foreach (string topic in testCase.Epilogue.SubscribedTopics)
                {
                    Assert.True(stubMqttClient.HasSubscribed(topic));
                }

                if (testCase.Epilogue.PublicationCount != null)
                {
                    int publicationCount = await stubMqttClient.GetPublicationCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.PublicationCount, publicationCount);
                }

                foreach (TestCasePublishedMessage publishedMessage in testCase.Epilogue.PublishedMessages)
                {
                    CheckPublishedMessage(publishedMessage, stubMqttClient, correlationIds, testCaseIndex);
                }

                if (testCase.Epilogue.AcknowledgementCount != null)
                {
                    int acknowledgementCount = await stubMqttClient.GetAcknowledgementCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.AcknowledgementCount, acknowledgementCount);
                }

                if (testCase.Epilogue.ExecutionCount != null)
                {
                    int executionCount = await commandExecutors.First().GetExecutionCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.ExecutionCount, executionCount);
                }

                foreach (KeyValuePair<int, int> kvp in testCase.Epilogue.ExecutionCounts)
                {
                    int executionCount = await commandExecutors[kvp.Key].GetExecutionCount().ConfigureAwait(false);
                    Assert.Equal(kvp.Value, executionCount);
                }

                try
                {
                    foreach (TestCommandExecutor commandExecutor in commandExecutors)
                    {
                        await commandExecutor.StopAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                        await commandExecutor.DisposeAsync();
                    }

                    if (testCase.Epilogue.Catch != null)
                    {
                        Assert.Fail($"Expected {testCase.Epilogue.Catch.ErrorKind} exception, but no exception thrown when stopping CommandExecutor");
                    }
                }
                catch (AkriMqttException exception)
                {
                    if (testCase.Epilogue.Catch == null)
                    {
                        Assert.Fail($"Unexpected exception thrown stopping CommandExecutor: {exception.Message}");
                    }

                    AkriMqttExceptionChecker.CheckException(testCase.Epilogue.Catch, exception);
                }
            }
            else
            {
                try
                {
                    foreach (TestCommandExecutor commandExecutor in commandExecutors)
                    {
                        await commandExecutor.StopAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                        await commandExecutor.DisposeAsync();
                    }
                }
                catch (AkriMqttException exception)
                {
                    Assert.Fail($"Unexpected exception thrown stopping CommandExecutor: {exception.Message}");
                }
            }
        }

        private async Task<TestCommandExecutor?> GetAndStartCommandExecutorAsync(IMqttPubSubClient mqttClient, TestCaseExecutor testCaseExecutor, Dictionary<string, AsyncCountdownEvent> countdownEvents, ConcurrentDictionary<string, AsyncAtomicInt> requestResponseSequencer, TestCaseCatch? testCaseCatch)
        {
            try
            {
                TestCommandExecutor commandExecutor = testCaseExecutor.CacheableDuration != null ?
                    new TestCommandExecutor(mqttClient, testCaseExecutor.CommandName!)
                    {
                        RequestTopicPattern = testCaseExecutor.RequestTopic!,
                        ExecutorId = testCaseExecutor.ExecutorId,
                        TopicNamespace = testCaseExecutor.TopicNamespace,
                        IsIdempotent = testCaseExecutor.Idempotent,
                        CacheableDuration = testCaseExecutor.CacheableDuration.ToTimeSpan(),
                        OnCommandReceived = null!,
                    } :
                    new TestCommandExecutor(mqttClient, testCaseExecutor.CommandName!)
                    {
                        RequestTopicPattern = testCaseExecutor.RequestTopic!,
                        ExecutorId = testCaseExecutor.ExecutorId,
                        TopicNamespace = testCaseExecutor.TopicNamespace,
                        IsIdempotent = testCaseExecutor.Idempotent,
                        OnCommandReceived = null!,
                    };

                commandExecutor.TopicTokenMap!["modelId"] = testCaseExecutor.ModelId!;
                commandExecutor.TopicTokenMap!["commandName"] = testCaseExecutor.CommandName!;
                commandExecutor.TopicTokenMap!["executorId"] = testCaseExecutor.ExecutorId!;

                if (testCaseExecutor.ExecutionTimeout != null)
                {
                    commandExecutor.ExecutionTimeout = testCaseExecutor.ExecutionTimeout.ToTimeSpan();
                }

                if (testCaseExecutor.ResponseMetadata.Any())
                {
                    commandExecutor.OnCommandReceived = async (extReq, ct) =>
                    {
                        await commandExecutor.Track().ConfigureAwait(false);
                        Object_Test_Response responsePayload = await ProcessRequest(extReq, testCaseExecutor, countdownEvents, requestResponseSequencer, ct).ConfigureAwait(false);

                        CommandResponseMetadata responseMetadata = new();
                        foreach (KeyValuePair<string, string?> kvp in testCaseExecutor.ResponseMetadata)
                        {
                            responseMetadata.UserData[kvp.Key] = kvp.Value ?? extReq.RequestMetadata.UserData[kvp.Key];
                        }

                        return new ExtendedResponse<Object_Test_Response>()
                        {
                            Response = responsePayload,
                            ResponseMetadata = responseMetadata,
                        };
                    };
                }
                else
                {
                    commandExecutor.OnCommandReceived = async (extReq, ct) =>
                    {
                        await commandExecutor.Track().ConfigureAwait(false);
                        Object_Test_Response responsePayload = await ProcessRequest(extReq, testCaseExecutor, countdownEvents, requestResponseSequencer, ct).ConfigureAwait(false);
                        return ExtendedResponse<Object_Test_Response>.CreateFromResponse(responsePayload);
                    };
                }

                await commandExecutor.StartAsync(testCaseExecutor.ExecutionConcurrency).WaitAsync(TestTimeout).ConfigureAwait(false);

                if (testCaseCatch != null)
                {
                    Assert.Fail($"Expected {testCaseCatch.ErrorKind} exception, but no exception thrown when initializing and starting CommandExecutor");
                }

                return commandExecutor;
            }
            catch (AkriMqttException exception)
            {
                if (testCaseCatch == null)
                {
                    Assert.Fail($"Unexpected exception thrown initializing or starting CommandExecutor: {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);

                return null;
            }
        }

        private async Task ReceiveRequestAsync(TestCaseActionReceiveRequest actionReceiveRequest, StubMqttClient stubMqttClient, ConcurrentDictionary<int, Guid?> invokerIds, ConcurrentDictionary<int, string?> correlationIds, ConcurrentDictionary<int, ushort> packetIds, int testCaseIndex)
        {
            Guid? invokerId = null;
            if (actionReceiveRequest.InvokerIndex != null)
            {
                if (!invokerIds.TryGetValue((int)actionReceiveRequest.InvokerIndex, out invokerId))
                {
                    invokerId = Guid.NewGuid();
                    invokerIds[(int)actionReceiveRequest.InvokerIndex] = invokerId;
                }
            }

            string? correlationId = null;
            if (actionReceiveRequest.CorrelationIndex != null)
            {
                correlationId = correlationIds.GetOrAdd((int)actionReceiveRequest.CorrelationIndex, actionReceiveRequest.CorrelationId ?? Guid.NewGuid().ToString());
            }

            ushort? specificPacketId = null;
            if (actionReceiveRequest.PacketIndex != null)
            {
                if (packetIds.TryGetValue((int)actionReceiveRequest.PacketIndex, out ushort extantPacketId))
                {
                    specificPacketId = extantPacketId;
                }
            }

            MqttApplicationMessageBuilder requestAppMsgBuilder = new MqttApplicationMessageBuilder().WithTopic(actionReceiveRequest.Topic);

            if (actionReceiveRequest.ContentType != null)
            {
                requestAppMsgBuilder.WithContentType(actionReceiveRequest.ContentType);
            }

            if (actionReceiveRequest.FormatIndicator != null)
            {
                requestAppMsgBuilder.WithPayloadFormatIndicator((MqttPayloadFormatIndicator)(int)actionReceiveRequest.FormatIndicator);
            }

            if (actionReceiveRequest.ResponseTopic != null)
            {
                requestAppMsgBuilder.WithResponseTopic(actionReceiveRequest.ResponseTopic);
            }

            if (actionReceiveRequest.Payload != null)
            {
                byte[]? payload =
                    actionReceiveRequest.BypassSerialization ? Encoding.UTF8.GetBytes(actionReceiveRequest.Payload) :
                    payloadSerializer.ToBytes(new Object_Test_Request { TestCaseIndex = testCaseIndex, Request = actionReceiveRequest.Payload });
                requestAppMsgBuilder.WithPayload(payload);
            }

            if (invokerId != null)
            {
                requestAppMsgBuilder.WithUserProperty(AkriSystemProperties.CommandInvokerId, ((Guid)invokerId!).ToString());
            }

            if (correlationId != null)
            {
                requestAppMsgBuilder.WithCorrelationData(Guid.TryParse(correlationId, out Guid correlationGuid) ? correlationGuid.ToByteArray() : Encoding.UTF8.GetBytes(correlationId));
            }

            if (actionReceiveRequest.Qos != null)
            {
                requestAppMsgBuilder.WithQualityOfServiceLevel((MqttQualityOfServiceLevel)actionReceiveRequest.Qos);
            }

            if (actionReceiveRequest.MessageExpiry != null)
            {
                requestAppMsgBuilder.WithMessageExpiryInterval((uint)actionReceiveRequest.MessageExpiry.ToTimeSpan().TotalSeconds);
            }

            foreach (KeyValuePair<string, string> kvp in actionReceiveRequest.Metadata)
            {
                requestAppMsgBuilder.WithUserProperty(kvp.Key, kvp.Value);
            }

            MqttApplicationMessage requestAppMsg = requestAppMsgBuilder.Build();

            ushort actualPacketId = await stubMqttClient.ReceiveMessageAsync(requestAppMsg, specificPacketId).WaitAsync(TestTimeout).ConfigureAwait(false);
            if (actionReceiveRequest.PacketIndex != null)
            {
                packetIds.TryAdd((int)actionReceiveRequest.PacketIndex, actualPacketId);
            }
        }

        private async Task AwaitAcknowledgementAsync(TestCaseActionAwaitAck actionAwaitAck, StubMqttClient stubMqttClient, ConcurrentDictionary<int, ushort> packetIds)
        {
            ushort packetId = await stubMqttClient.AwaitAcknowledgementAsync().WaitAsync(TestTimeout).ConfigureAwait(false);

            if (actionAwaitAck.PacketIndex != null)
            {
                Assert.True(packetIds.TryGetValue((int)actionAwaitAck.PacketIndex, out ushort extantPacketId));
                Assert.Equal(extantPacketId, packetId);
            }
        }

        private async Task AwaitPublishAsync(TestCaseActionAwaitPublish actionAwaitPublish, StubMqttClient stubMqttClient, ConcurrentDictionary<int, string?> correlationIds)
        {
            byte[] correlationId = await stubMqttClient.AwaitPublishAsync().WaitAsync(TestTimeout).ConfigureAwait(false);

            if (actionAwaitPublish.CorrelationIndex != null)
            {
                Assert.True(correlationIds.TryGetValue((int)actionAwaitPublish.CorrelationIndex, out string? extantCorrelationId));
                Assert.NotNull(extantCorrelationId);
                Assert.Equal(Guid.TryParse(extantCorrelationId, out Guid extantGuid) ? extantGuid.ToByteArray() : Encoding.UTF8.GetBytes(extantCorrelationId), correlationId);
            }
        }

        private async Task SyncEventAsync(TestCaseActionSync actionSync, Dictionary<string, AsyncCountdownEvent> countdownEvents)
        {
            if (actionSync.WaitEvent != null)
            {
                await countdownEvents[actionSync.WaitEvent].WaitAsync(TestTimeout).ConfigureAwait(false);
            }

            if (actionSync.SignalEvent != null)
            {
                await countdownEvents[actionSync.SignalEvent].SignalAsync().ConfigureAwait(false);
            }
        }

        private Task SleepAsync(TestCaseActionSleep actionSleep)
        {
            return freezableWallClock.WaitForAsync(actionSleep.Duration!.ToTimeSpan()).WaitAsync(TestTimeout);
        }

        private Task DisconnectAsync(StubMqttClient stubMqttClient)
        {
            return stubMqttClient.DisconnectAsync(new MqttClientDisconnectOptions());
        }

        private Task<int> FreezeTimeAsync()
        {
            return freezableWallClock.FreezeTimeAsync();
        }

        private Task UnfreezeTimeAsync(int freezeTicket)
        {
            return freezableWallClock.UnfreezeTimeAsync(freezeTicket);
        }

        private void CheckPublishedMessage(TestCasePublishedMessage publishedMessage, StubMqttClient stubMqttClient, ConcurrentDictionary<int, string?> correlationIds, int testCaseIndex)
        {
            string? correlationId = null;
            if (publishedMessage.CorrelationIndex != null)
            {
                Assert.True(correlationIds.TryGetValue((int)publishedMessage.CorrelationIndex, out correlationId));
            }

            byte[]? lookupKey = correlationId != null ?
                Guid.TryParse(correlationId, out Guid correlationGuid) ? correlationGuid.ToByteArray() : Encoding.UTF8.GetBytes(correlationId) :
                null;
            MqttApplicationMessage? appMsg = stubMqttClient.GetPublishedMessage(lookupKey);
            Assert.NotNull(appMsg);

            if (publishedMessage.Topic != null)
            {
                Assert.Equal(publishedMessage.Topic, appMsg.Topic);
            }

            if (publishedMessage.Payload == null)
            {
                Assert.Null(appMsg.PayloadSegment.Array);
            }
            else if (publishedMessage.Payload is string payload)
            {
                Assert.Equal(payloadSerializer.ToBytes(new Object_Test_Response { TestCaseIndex = testCaseIndex, Response = payload }), appMsg.PayloadSegment.Array);
            }

            foreach (KeyValuePair<string, string?> kvp in publishedMessage.Metadata)
            {
                if (kvp.Value != null)
                {
                    Assert.True(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(kvp.Key, out string? value));
                    Assert.Equal(kvp.Value, value);
                }
                else
                {
                    Assert.False(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(kvp.Key, out string? value), $"header {kvp.Key} unexpectedly present with value '{value}'");
                }
            }

            if (publishedMessage.CommandStatus == null)
            {
                Assert.DoesNotContain(appMsg.UserProperties, p => p.Name == AkriSystemProperties.Status);
            }
            else if (publishedMessage.CommandStatus is int expectedStatus || int.TryParse(publishedMessage.CommandStatus.ToString(), out expectedStatus))
            {
                Assert.True(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(AkriSystemProperties.Status, out string? cmdStatus));
                Assert.Equal(expectedStatus.ToString(CultureInfo.InvariantCulture), cmdStatus);
            }

            if (publishedMessage.IsApplicationError == true)
            {
                Assert.True(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) && isAppError?.ToLower() == "true");
            }
            else if (publishedMessage.IsApplicationError == false)
            {
                Assert.True(!MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(AkriSystemProperties.IsApplicationError, out string? isAppError) || isAppError?.ToLower() == "false");
            }
        }

        private static async Task<Object_Test_Response> ProcessRequest(ExtendedRequest<Object_Test_Request> extReq, TestCaseExecutor testCaseExecutor, Dictionary<string, AsyncCountdownEvent> countdownEvents, ConcurrentDictionary<string, AsyncAtomicInt> requestResponseSequencer, CancellationToken cancellationToken)
        {
            foreach (TestCaseSync testCaseSync in testCaseExecutor.Sync)
            {
                if (testCaseSync.WaitEvent != null)
                {
                    await countdownEvents[testCaseSync.WaitEvent].WaitAsync(cancellationToken).WaitAsync(TestTimeout).ConfigureAwait(false);
                }

                if (testCaseSync.SignalEvent != null)
                {
                    await countdownEvents[testCaseSync.SignalEvent].SignalAsync().ConfigureAwait(false);
                }
            }

            if (testCaseExecutor.RaiseError != null && testCaseExecutor.RaiseError.Kind != TestErrorKind.None)
            {
                throw testCaseExecutor.RaiseError.Kind == TestErrorKind.Content ?
                    new InvocationException(testCaseExecutor.RaiseError.Message, testCaseExecutor.RaiseError.PropertyName, testCaseExecutor.RaiseError.PropertyValue) :
                    new ApplicationException(testCaseExecutor.RaiseError.Message);
            }

            if (extReq.Request.Request != null && testCaseExecutor.RequestResponsesMap.TryGetValue(extReq.Request.Request, out string[]? responses) && responses.Length > 0)
            {
                int index = 0;
                AsyncAtomicInt sequencer = new AsyncAtomicInt(index);
                if (!requestResponseSequencer.TryAdd(extReq.Request.Request, sequencer))
                {
                    index = await requestResponseSequencer[extReq.Request.Request].Increment().ConfigureAwait(false) % responses.Length;
                }

                return new Object_Test_Response { TestCaseIndex = extReq.Request.TestCaseIndex, Response = responses[index] };
            }
            else
            {
                return null!;
            }
        }

        private static string PascalToKebabCase(string name)
        {
            StringBuilder builder = new();
            try
            {
                char c = '\0';
                foreach (char c2 in name)
                {
                    if (char.IsUpper(c2) && !char.IsUpper(c) && c != 0 && c != '-')
                    {
                        builder.Append('-');
                    }

                    builder.Append(char.ToLowerInvariant(c2));
                    c = c2;
                }

                return builder.ToString();
            }
            finally
            {
                builder.Length = 0;
            }
        }
    }
}
