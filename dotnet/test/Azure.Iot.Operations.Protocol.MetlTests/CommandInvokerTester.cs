// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using Azure.Iot.Operations.Protocol.RPC;
using Tomlyn;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Azure.Iot.Operations.Mqtt.Converters;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class CommandInvokerTester
    {
        private const string testCasesPath = "../../../../../../eng/test/test-cases";
        private const string invokerCasesPath = $"{testCasesPath}/Protocol/CommandInvoker";
        private const string defaultsFilePath = $"{testCasesPath}/Protocol/CommandInvoker/defaults.toml";

        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly HashSet<string> problematicTestCases = new HashSet<string>
        {
        };

        private static IDeserializer yamlDeserializer;
        private static AsyncAtomicInt TestCaseIndex = new(0);
        private static FreezableWallClock freezableWallClock;

        static CommandInvokerTester()
        {
            yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
                .WithTypeDiscriminatingNodeDeserializer(options =>
                {
                    options.AddKeyValueTypeDiscriminator<TestCaseAction>("action",
                        ("invoke command", typeof(TestCaseActionInvokeCommand)),
                        ("await invocation", typeof(TestCaseActionAwaitInvocation)),
                        ("receive response", typeof(TestCaseActionReceiveResponse)),
                        ("await acknowledgement", typeof(TestCaseActionAwaitAck)),
                        ("await publish", typeof(TestCaseActionAwaitPublish)),
                        ("sleep", typeof(TestCaseActionSleep)),
                        ("disconnect", typeof(TestCaseActionDisconnect)),
                        ("freeze time", typeof(TestCaseActionFreezeTime)),
                        ("unfreeze time", typeof(TestCaseActionUnfreezeTime)));
                })
                .Build();

            if (File.Exists(defaultsFilePath))
            {
                DefaultTestCase defaultTestCase = Toml.ToModel<DefaultTestCase>(File.ReadAllText(defaultsFilePath), defaultsFilePath, new TomlModelOptions { ConvertPropertyName = CaseConverter.PascalToKebabCase });

                TestCaseSerializer.DefaultOutContentType = defaultTestCase.Prologue.Invoker.Serializer.OutContentType;
                TestCaseSerializer.DefaultAcceptContentTypes = defaultTestCase.Prologue.Invoker.Serializer.AcceptContentTypes;
                TestCaseSerializer.DefaultIndicateCharacterData = defaultTestCase.Prologue.Invoker.Serializer.IndicateCharacterData;
                TestCaseSerializer.DefaultAllowCharacterData = defaultTestCase.Prologue.Invoker.Serializer.AllowCharacterData;
                TestCaseSerializer.DefaultFailDeserialization = defaultTestCase.Prologue.Invoker.Serializer.FailDeserialization;

                TestCaseInvoker.DefaultCommandName = defaultTestCase.Prologue.Invoker.CommandName;
                TestCaseInvoker.DefaultRequestTopic = defaultTestCase.Prologue.Invoker.RequestTopic;
                TestCaseInvoker.DefaultTopicNamespace = defaultTestCase.Prologue.Invoker.TopicNamespace;
                TestCaseInvoker.DefaultResponseTopicPrefix = defaultTestCase.Prologue.Invoker.ResponseTopicPrefix;
                TestCaseInvoker.DefaultResponseTopicSuffix = defaultTestCase.Prologue.Invoker.ResponseTopicSuffix;

                TestCaseActionInvokeCommand.DefaultCommandName = defaultTestCase.Actions.InvokeCommand.CommandName;
                TestCaseActionInvokeCommand.DefaultRequestValue = defaultTestCase.Actions.InvokeCommand.RequestValue;
                TestCaseActionInvokeCommand.DefaultTimeout = defaultTestCase.Actions.InvokeCommand.Timeout;

                TestCaseActionReceiveResponse.DefaultTopic = defaultTestCase.Actions.ReceiveResponse.Topic;
                TestCaseActionReceiveResponse.DefaultPayload = defaultTestCase.Actions.ReceiveResponse.Payload;
                TestCaseActionReceiveResponse.DefaultContentType = defaultTestCase.Actions.ReceiveResponse.ContentType;
                TestCaseActionReceiveResponse.DefaultFormatIndicator = defaultTestCase.Actions.ReceiveResponse.FormatIndicator;
                TestCaseActionReceiveResponse.DefaultCorrelationIndex = defaultTestCase.Actions.ReceiveResponse.CorrelationIndex;
                TestCaseActionReceiveResponse.DefaultQos = defaultTestCase.Actions.ReceiveResponse.Qos;
                TestCaseActionReceiveResponse.DefaultMessageExpiry = defaultTestCase.Actions.ReceiveResponse.MessageExpiry;
                TestCaseActionReceiveResponse.DefaultStatus = defaultTestCase.Actions.ReceiveResponse.Status;
                TestCaseActionReceiveResponse.DefaultStatusMessage = defaultTestCase.Actions.ReceiveResponse.StatusMessage;
                TestCaseActionReceiveResponse.DefaultIsApplicationError = defaultTestCase.Actions.ReceiveResponse.IsApplicationError;
                TestCaseActionReceiveResponse.DefaultInvalidPropertyName = defaultTestCase.Actions.ReceiveResponse.InvalidPropertyName;
                TestCaseActionReceiveResponse.DefaultInvalidPropertyValue = defaultTestCase.Actions.ReceiveResponse.InvalidPropertyValue;
            }

            freezableWallClock = new FreezableWallClock();
            TestCommandInvoker.WallClock = freezableWallClock;
        }

        public static IEnumerable<object[]> GetAllCommandInvokerCases()
        {
            foreach (string testCasePath in Directory.GetFiles(invokerCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{invokerCasesPath}/{testCaseName}.yaml"))
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

        public static IEnumerable<object[]> GetRestrictedCommandInvokerCases()
        {
            foreach (string testCasePath in Directory.GetFiles(invokerCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{invokerCasesPath}/{testCaseName}.yaml"))
                {
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
        [MemberData(nameof(GetAllCommandInvokerCases))]
        public Task TestCommandInvokerWithSessionClient(string testCaseName)
        {
            return TestCommandInvokerProtocol(testCaseName, includeSessionClient: true);
        }

        [Theory]
        [MemberData(nameof(GetRestrictedCommandInvokerCases))]
        public Task TestCommandInvokerStandalone(string testCaseName)
        {
            return TestCommandInvokerProtocol(testCaseName, includeSessionClient: false);
        }

        private async Task TestCommandInvokerProtocol(string testCaseName, bool includeSessionClient)
        {
            int testCaseIndex = await TestCaseIndex.Increment().ConfigureAwait(false);

            TestCase testCase;
            using (StreamReader streamReader = File.OpenText($"{invokerCasesPath}/{testCaseName}.yaml"))
            {
                testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
            }

            Dictionary<string, TestCommandInvoker> commandInvokers = new();

            string clientIdPrefix = includeSessionClient ? "Session" : "Standalone";
            string mqttClientId = testCase.Prologue?.MqttConfig?.ClientId ?? $"{clientIdPrefix}InvokerTestClient{testCaseIndex}";
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

            foreach (TestCaseInvoker testCaseInvoker in testCase.Prologue?.Invokers ?? new List<TestCaseInvoker>())
            {
                bool isLast = ReferenceEquals(testCaseInvoker, testCase.Prologue?.Invokers.Last());
                TestCommandInvoker? commandInvoker = await GetCommandInvoker(compositeMqttClient, testCaseInvoker, isLast ? testCase.Prologue?.Catch : null);
                if (commandInvoker == null)
                {
                    return;
                }

                commandInvokers[testCaseInvoker.CommandName!] = commandInvoker;
            }

            ConcurrentDictionary<int, Task<ExtendedResponse<string>>> invocationTasks = new();
            ConcurrentDictionary<int, Guid?> correlationIds = new();
            ConcurrentDictionary<int, ushort> packetIds = new();
            int freezeTicket = -1;

            foreach (TestCaseAction action in testCase.Actions)
            {
                switch (action)
                {
                    case TestCaseActionInvokeCommand actionInvokeCommand:
                        InvokeCommandAsync(actionInvokeCommand, commandInvokers, invocationTasks);
                        break;
                    case TestCaseActionAwaitInvocation actionAwaitInvocation:
                        await AwaitInvocationAsync(actionAwaitInvocation, invocationTasks).ConfigureAwait(false);
                        break;
                    case TestCaseActionReceiveResponse actionReceiveResponse:
                        await ReceiveResponseAsync(actionReceiveResponse, stubMqttClient, correlationIds, packetIds).ConfigureAwait(false);
                        break;
                    case TestCaseActionAwaitAck actionAwaitAck:
                        await AwaitAcknowledgementAsync(actionAwaitAck, stubMqttClient, packetIds).ConfigureAwait(false);
                        break;
                    case TestCaseActionAwaitPublish actionAwaitPublish:
                        await AwaitPublishAsync(actionAwaitPublish, stubMqttClient, correlationIds).ConfigureAwait(false);
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
                    CheckPublishedMessage(publishedMessage, stubMqttClient, correlationIds);
                }

                if (testCase.Epilogue.AcknowledgementCount != null)
                {
                    int acknowledgementCount = await stubMqttClient.GetAcknowledgementCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.AcknowledgementCount, acknowledgementCount);
                }
            }
        }

        private async Task<TestCommandInvoker?> GetCommandInvoker(IMqttPubSubClient mqttClient, TestCaseInvoker testCaseInvoker, TestCaseCatch? testCaseCatch)
        {
            try
            {
                TestSerializer testSerializer = new TestSerializer(testCaseInvoker.Serializer);

                TestCommandInvoker commandInvoker = new TestCommandInvoker(new ApplicationContext(), mqttClient, testCaseInvoker.CommandName!, testSerializer)
                {
                    RequestTopicPattern = testCaseInvoker.RequestTopic!,
                    TopicNamespace = testCaseInvoker.TopicNamespace,
                    ResponseTopicPrefix = testCaseInvoker.ResponseTopicPrefix,
                    ResponseTopicSuffix = testCaseInvoker.ResponseTopicSuffix,
                    GetResponseTopic = testCaseInvoker.ResponseTopicMap != null ? (reqTopic) => testCaseInvoker.ResponseTopicMap[reqTopic]! : null,
                };

                if (testCaseInvoker.TopicTokenMap != null)
                {
                    foreach (KeyValuePair<string, string> kvp in testCaseInvoker.TopicTokenMap)
                    {
                        commandInvoker.TopicTokenMap![kvp.Key] = kvp.Value;
                    }
                }

                if (testCaseCatch != null)
                {
                    // CommandInvoker has no Start method, so if an exception is expected, Invoke may be needed to trigger it.
                    try
                    {
                        await commandInvoker.InvokeCommandAsync(TestCaseActionInvokeCommand.DefaultRequestValue!).WaitAsync(TestTimeout);
                    }
                    catch (AkriMqttException exception)
                    {
                        if (exception.Kind != AkriMqttErrorKind.Cancellation)
                        {
                            AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);
                            return null;
                        }
                    }

                    Assert.Fail($"Expected {testCaseCatch.ErrorKind} exception, but no exception thrown when initializing CommandInvoker");
                }

                return commandInvoker;
            }
            catch (AkriMqttException exception)
            {
                if (testCaseCatch == null)
                {
                    Assert.Fail($"Unexpected exception thrown initializing CommandInvoker: {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);
                return null;
            }
        }

        private void InvokeCommandAsync(TestCaseActionInvokeCommand actionInvokeCommand, Dictionary<string, TestCommandInvoker> commandInvokers, ConcurrentDictionary<int, Task<ExtendedResponse<string>>> invocationTasks)
        {
            CommandRequestMetadata? metadata = null;
            if (actionInvokeCommand.Metadata != null)
            {
                metadata = new CommandRequestMetadata();
                foreach (KeyValuePair<string, string> kvp in actionInvokeCommand.Metadata)
                {
                    metadata.UserData[kvp.Key] = kvp.Value;
                }
            }

            invocationTasks[(int)actionInvokeCommand.InvocationIndex!] = commandInvokers[actionInvokeCommand.CommandName!].InvokeCommandAsync(actionInvokeCommand.RequestValue!, metadata, actionInvokeCommand.TopicTokenMap, actionInvokeCommand.Timeout?.ToTimeSpan());
        }

        private async Task AwaitInvocationAsync(TestCaseActionAwaitInvocation actionAwaitInvocation, ConcurrentDictionary<int, Task<ExtendedResponse<string>>> invocationTasks)
        {
            try
            {
                ExtendedResponse<string> extResp = await invocationTasks[(int)actionAwaitInvocation.InvocationIndex!].WaitAsync(TestTimeout).ConfigureAwait(false);

                if (actionAwaitInvocation.Catch != null)
                {
                    Assert.Fail($"Expected {actionAwaitInvocation.Catch.ErrorKind} exception, but no exception thrown when awaiting CommandInvoker.InvokeCommandAsync()");
                }

                if (actionAwaitInvocation.ResponseValue == null)
                {
                    Assert.Null(extResp.Response);
                }
                else if (actionAwaitInvocation.ResponseValue is string response)
                {
                    Assert.Equal(response, extResp.Response);
                }

                if (actionAwaitInvocation.Metadata != null)
                {
                    Assert.NotNull(extResp.ResponseMetadata);

                    foreach (KeyValuePair<string, string> kvp in actionAwaitInvocation.Metadata)
                    {
                        Assert.True(extResp.ResponseMetadata.UserData.TryGetValue(kvp.Key, out string? value));
                        Assert.Equal(kvp.Value, value);
                    }
                }
            }
            catch (AkriMqttException exception)
            {
                if (actionAwaitInvocation.Catch == null)
                {
                    Assert.Fail($"Unexpected exception thrown when awaiting CommandInvoker.InvokeCommandAsync(): {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(actionAwaitInvocation.Catch, exception);
            }
        }

        private async Task ReceiveResponseAsync(TestCaseActionReceiveResponse actionReceiveResponse, StubMqttClient stubMqttClient, ConcurrentDictionary<int, Guid?> correlationIds, ConcurrentDictionary<int, ushort> packetIds)
        {
            Guid? correlationId = actionReceiveResponse.CorrelationIndex != null ? correlationIds[(int)actionReceiveResponse.CorrelationIndex] : null;

            ushort? specificPacketId = null;
            if (actionReceiveResponse.PacketIndex != null)
            {
                if (packetIds.TryGetValue((int)actionReceiveResponse.PacketIndex, out ushort extantPacketId))
                {
                    specificPacketId = extantPacketId;
                }
            }

            MQTTnet.MqttApplicationMessageBuilder responseAppMsgBuilder = new MQTTnet.MqttApplicationMessageBuilder().WithTopic(actionReceiveResponse.Topic);

            if (actionReceiveResponse.ContentType != null)
            {
                responseAppMsgBuilder.WithContentType(actionReceiveResponse.ContentType);
            }

            if (actionReceiveResponse.FormatIndicator != null)
            {
                responseAppMsgBuilder.WithPayloadFormatIndicator((MQTTnet.Protocol.MqttPayloadFormatIndicator)(int)actionReceiveResponse.FormatIndicator);
            }

            if (actionReceiveResponse.Payload != null)
            {
                byte[]? payload = Encoding.UTF8.GetBytes(actionReceiveResponse.Payload);
                responseAppMsgBuilder.WithPayload(payload);
            }

            if (correlationId != null)
            {
                responseAppMsgBuilder.WithCorrelationData(correlationId.Value.ToByteArray());
            }

            if (actionReceiveResponse.Qos != null)
            {
                responseAppMsgBuilder.WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)actionReceiveResponse.Qos);
            }

            if (actionReceiveResponse.MessageExpiry != null)
            {
                responseAppMsgBuilder.WithMessageExpiryInterval((uint)actionReceiveResponse.MessageExpiry.ToTimeSpan().TotalSeconds);
            }

            foreach (KeyValuePair<string, string> kvp in actionReceiveResponse.Metadata)
            {
                responseAppMsgBuilder.WithUserProperty(kvp.Key, kvp.Value);
            }

            if (actionReceiveResponse.Status != null)
            {
                responseAppMsgBuilder.WithUserProperty(AkriSystemProperties.Status, actionReceiveResponse.Status);
            }

            if (actionReceiveResponse.StatusMessage != null)
            {
                responseAppMsgBuilder.WithUserProperty(AkriSystemProperties.StatusMessage, actionReceiveResponse.StatusMessage);
            }

            if (actionReceiveResponse.IsApplicationError != null)
            {
                responseAppMsgBuilder.WithUserProperty(AkriSystemProperties.IsApplicationError, actionReceiveResponse.IsApplicationError);
            }

            if (actionReceiveResponse.InvalidPropertyName != null)
            {
                responseAppMsgBuilder.WithUserProperty(AkriSystemProperties.InvalidPropertyName, actionReceiveResponse.InvalidPropertyName);
            }

            if (actionReceiveResponse.InvalidPropertyValue != null)
            {
                responseAppMsgBuilder.WithUserProperty(AkriSystemProperties.InvalidPropertyValue, actionReceiveResponse.InvalidPropertyValue);
            }

            MQTTnet.MqttApplicationMessage responseAppMsg = responseAppMsgBuilder.Build();

            ushort actualPacketId = await stubMqttClient.ReceiveMessageAsync(responseAppMsg, specificPacketId).WaitAsync(TestTimeout).ConfigureAwait(false);
            if (actionReceiveResponse.PacketIndex != null)
            {
                packetIds.TryAdd((int)actionReceiveResponse.PacketIndex, actualPacketId);
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

        private async Task AwaitPublishAsync(TestCaseActionAwaitPublish actionAwaitPublish, StubMqttClient stubMqttClient, ConcurrentDictionary<int, Guid?> correlationIds)
        {
            byte[]? correlationId = await stubMqttClient.AwaitPublishAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
            if (actionAwaitPublish.CorrelationIndex != null)
            {
                correlationIds[(int)actionAwaitPublish.CorrelationIndex] = new Guid(correlationId);
            }
        }

        private Task SleepAsync(TestCaseActionSleep actionSleep)
        {
            return freezableWallClock.WaitForAsync(actionSleep.Duration!.ToTimeSpan()).WaitAsync(TestTimeout);
        }

        private Task DisconnectAsync(StubMqttClient stubMqttClient)
        {
            return stubMqttClient.DisconnectAsync(new MQTTnet.MqttClientDisconnectOptions());
        }

        private Task<int> FreezeTimeAsync()
        {
            return freezableWallClock.FreezeTimeAsync();
        }

        private Task UnfreezeTimeAsync(int freezeTicket)
        {
            return freezableWallClock.UnfreezeTimeAsync(freezeTicket);
        }

        private void CheckPublishedMessage(TestCasePublishedMessage publishedMessage, StubMqttClient stubMqttClient, ConcurrentDictionary<int, Guid?> correlationIds)
        {
            Guid? correlationId = null;
            if (publishedMessage.CorrelationIndex != null)
            {
                Assert.True(correlationIds.TryGetValue((int)publishedMessage.CorrelationIndex, out correlationId));
            }

            byte[]? lookupKey = correlationId != null ? correlationId.Value.ToByteArray() : null;
            MQTTnet.MqttApplicationMessage? appMsg = stubMqttClient.GetPublishedMessage(lookupKey);
            Assert.NotNull(appMsg);

            if (publishedMessage.Topic != null)
            {
                Assert.Equal(publishedMessage.Topic, appMsg.Topic);
            }

            if (publishedMessage.Payload == null)
            {
                Assert.True(appMsg.Payload.IsEmpty);
            }
            else if (publishedMessage.Payload is string payload)
            {
                Assert.False(appMsg.Payload.IsEmpty);
                Assert.Equal(payload, Encoding.UTF8.GetString(appMsg.Payload));
            }

            if (publishedMessage.ContentType != null)
            {
                Assert.Equal(publishedMessage.ContentType, appMsg.ContentType);
            }

            if (publishedMessage.FormatIndicator != null)
            {
                Assert.Equal(publishedMessage.FormatIndicator, (int)appMsg.PayloadFormatIndicator);
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

            if (publishedMessage.SourceId != null)
            {
                Assert.True(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(AkriSystemProperties.SourceId, out string? sourceId));
                Assert.Equal(publishedMessage.SourceId, sourceId);
            }

            if (publishedMessage.Expiry != null)
            {
                Assert.Equal((uint)publishedMessage.Expiry, appMsg.MessageExpiryInterval);
            }
        }
    }
}
