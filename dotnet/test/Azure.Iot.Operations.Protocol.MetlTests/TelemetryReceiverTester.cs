// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.Threading;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using Tomlyn;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TestModel.dtmi_test_TestModel__1;
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Converters;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TelemetryReceiverTester
    {
        private const string testCasesPath = "../../../../../../eng/test/test-cases";
        private const string receiverCasesPath = $"{testCasesPath}/Protocol/TelemetryReceiver";
        private const string defaultsFilePath = $"{testCasesPath}/Protocol/TelemetryReceiver/defaults.toml";

        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly HashSet<string> problematicTestCases = new HashSet<string>
        {
            "TelemetryReceiverReceivesWithCloudEvent_Success"
        };

        private static IDeserializer yamlDeserializer;
        private static AsyncAtomicInt TestCaseIndex = new(0);
        private static FreezableWallClock freezableWallClock;
        private static IPayloadSerializer payloadSerializer;

        static TelemetryReceiverTester()
        {
            yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
                .WithTypeDiscriminatingNodeDeserializer(options =>
                {
                    options.AddKeyValueTypeDiscriminator<TestCaseAction>("action",
                        ("receive telemetry", typeof(TestCaseActionReceiveTelemetry)),
                        ("await acknowledgement", typeof(TestCaseActionAwaitAck)),
                        ("sleep", typeof(TestCaseActionSleep)),
                        ("disconnect", typeof(TestCaseActionDisconnect)),
                        ("freeze time", typeof(TestCaseActionFreezeTime)),
                        ("unfreeze time", typeof(TestCaseActionUnfreezeTime)));
                })
                .Build();

            if (File.Exists(defaultsFilePath))
            {
                DefaultTestCase defaultTestCase = Toml.ToModel<DefaultTestCase>(File.ReadAllText(defaultsFilePath), defaultsFilePath, new TomlModelOptions { ConvertPropertyName = CaseConverter.PascalToKebabCase });

                TestCaseReceiver.DefaultTelemetryTopic = defaultTestCase.Prologue.Receiver.TelemetryTopic;
                TestCaseReceiver.DefaultTopicNamespace = defaultTestCase.Prologue.Receiver.TopicNamespace;

                TestCaseActionReceiveTelemetry.DefaultTopic = defaultTestCase.Actions.ReceiveTelemetry.Topic;
                TestCaseActionReceiveTelemetry.DefaultPayload = defaultTestCase.Actions.ReceiveTelemetry.Payload;
                TestCaseActionReceiveTelemetry.DefaultContentType = defaultTestCase.Actions.ReceiveTelemetry.ContentType;
                TestCaseActionReceiveTelemetry.DefaultFormatIndicator = defaultTestCase.Actions.ReceiveTelemetry.FormatIndicator;
                TestCaseActionReceiveTelemetry.DefaultQos = defaultTestCase.Actions.ReceiveTelemetry.Qos;
                TestCaseActionReceiveTelemetry.DefaultMessageExpiry = defaultTestCase.Actions.ReceiveTelemetry.MessageExpiry;
                TestCaseActionReceiveTelemetry.DefaultSourceIndex = defaultTestCase.Actions.ReceiveTelemetry.SourceIndex;
            }

            freezableWallClock = new FreezableWallClock();
            TestTelemetryReceiver.WallClock = freezableWallClock;

            payloadSerializer = new Utf8JsonSerializer();
        }

        public static IEnumerable<object[]> GetAllTelemetryReceiverCases()
        {
            foreach (string testCasePath in Directory.GetFiles(receiverCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{receiverCasesPath}/{testCaseName}.yaml"))
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

        public static IEnumerable<object[]> GetRestrictedTelemetryReceiverCases()
        {
            foreach (string testCasePath in Directory.GetFiles(receiverCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{receiverCasesPath}/{testCaseName}.yaml"))
                {
                    Trace.TraceInformation($"Deserializing {receiverCasesPath}/{testCaseName}.yaml");
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
        [MemberData(nameof(GetRestrictedTelemetryReceiverCases))]
        public Task TestTelemetryReceiverWithSessionClient(string testCaseName)
        {
            return TestTelemetryReceiverProtocol(testCaseName, includeSessionClient: true);
        }

        [Theory]
        [MemberData(nameof(GetRestrictedTelemetryReceiverCases))]
        public Task TestTelemetryReceiverStandalone(string testCaseName)
        {
            return TestTelemetryReceiverProtocol(testCaseName, includeSessionClient: false);
        }

        private async Task TestTelemetryReceiverProtocol(string testCaseName, bool includeSessionClient)
        {
            int testCaseIndex = await TestCaseIndex.Increment().ConfigureAwait(false);

            TestCase testCase;
            using (StreamReader streamReader = File.OpenText($"{receiverCasesPath}/{testCaseName}.yaml"))
            {
                testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
            }

            List<TestTelemetryReceiver> telemetryReceivers = new();

            string clientIdPrefix = includeSessionClient ? "Session" : "Standalone";
            string mqttClientId = testCase.Prologue?.MqttConfig?.ClientId ?? $"{clientIdPrefix}ReceiverTestClient{testCaseIndex}";
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

            AsyncQueue<ReceivedTelemetry> receivedTelemetries = new();

            foreach (TestCaseReceiver testCaseReceiver in testCase.Prologue?.Receivers ?? new List<TestCaseReceiver>())
            {
                bool isLast = ReferenceEquals(testCaseReceiver, testCase.Prologue?.Receivers.Last());
                TestTelemetryReceiver? telemetryReceiver = await GetAndStartTelemetryReceiverAsync(compositeMqttClient, testCaseReceiver, isLast ? testCase.Prologue?.Catch : null, receivedTelemetries).ConfigureAwait(false);
                if (telemetryReceiver == null)
                {
                    return;
                }

                telemetryReceivers.Add(telemetryReceiver);
            }

            ConcurrentDictionary<int, string> sourceIds = new();
            ConcurrentDictionary<int, ushort> packetIds = new();
            int freezeTicket = -1;

            try
            {
                foreach (TestCaseAction action in testCase.Actions)
                {
                    switch (action)
                    {
                        case TestCaseActionReceiveTelemetry actionReceiveTelemetry:
                            await ReceiveTelemetryAsync(actionReceiveTelemetry, stubMqttClient, sourceIds, packetIds, testCaseIndex).ConfigureAwait(false);
                            break;
                        case TestCaseActionAwaitAck actionAwaitAck:
                            await AwaitAcknowledgementAsync(actionAwaitAck, stubMqttClient, packetIds).ConfigureAwait(false);
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

                if (testCase.Epilogue.AcknowledgementCount != null)
                {
                    int acknowledgementCount = await stubMqttClient.GetAcknowledgementCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.AcknowledgementCount, acknowledgementCount);
                }

                if (testCase.Epilogue.TelemetryCount != null)
                {
                    int telemetryCount = await telemetryReceivers.First().GetTelemetryCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.TelemetryCount, telemetryCount);
                }

                foreach (KeyValuePair<int, int> kvp in testCase.Epilogue.TelemetryCounts)
                {
                    int telemetryCount = await telemetryReceivers[kvp.Key].GetTelemetryCount().ConfigureAwait(false);
                    Assert.Equal(kvp.Value, telemetryCount);
                }

                foreach (TestCaseReceivedTelemetry receivedTelemetry in testCase.Epilogue.ReceivedTelemetries)
                {
                    CheckReceivedTelemetry(receivedTelemetry, testCaseIndex, receivedTelemetries, sourceIds);
                }

                try
                {
                    foreach (TestTelemetryReceiver telemetryReceiver in telemetryReceivers)
                    {
                        await telemetryReceiver.StopAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                        await telemetryReceiver.DisposeAsync();
                    }

                    if (testCase.Epilogue.Catch != null)
                    {
                        Assert.Fail($"Expected {testCase.Epilogue.Catch.ErrorKind} exception, but no exception thrown when stopping TelemetryReceiver");
                    }
                }
                catch (AkriMqttException exception)
                {
                    if (testCase.Epilogue.Catch == null)
                    {
                        Assert.Fail($"Unexpected exception thrown stopping TelemetryReceiver: {exception.Message}");
                    }

                    AkriMqttExceptionChecker.CheckException(testCase.Epilogue.Catch, exception);
                }
            }
            else
            {
                try
                {
                    foreach (TestTelemetryReceiver telemetryReceiver in telemetryReceivers)
                    {
                        await telemetryReceiver.StopAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                        await telemetryReceiver.DisposeAsync();
                    }
                }
                catch (AkriMqttException exception)
                {
                    Assert.Fail($"Unexpected exception thrown stopping TelemetryReceiver: {exception.Message}");
                }
            }
        }

        private async Task<TestTelemetryReceiver?> GetAndStartTelemetryReceiverAsync(IMqttPubSubClient mqttClient, TestCaseReceiver testCaseReceiver, TestCaseCatch? testCaseCatch, AsyncQueue<ReceivedTelemetry> receivedTelemetries)
        {
            try
            {
                TestTelemetryReceiver telemetryReceiver = new TestTelemetryReceiver(mqttClient)
                {
                    TopicPattern = testCaseReceiver.TelemetryTopic!,
                    TopicNamespace = testCaseReceiver.TopicNamespace,
                    OnTelemetryReceived = null!,
                };

                if (testCaseReceiver.TopicTokenMap != null)
                {
                    foreach (KeyValuePair<string, string> kvp in testCaseReceiver.TopicTokenMap)
                    {
                        telemetryReceiver.TopicTokenMap![kvp.Key] = kvp.Value;
                    }
                }

                telemetryReceiver.OnTelemetryReceived = async (sourceId, telemetry, metadata) =>
                {
                    await telemetryReceiver.Track().ConfigureAwait(false);
                    ProcessTelemetry(sourceId, telemetry, metadata, testCaseReceiver, receivedTelemetries);
                };

                await telemetryReceiver.StartAsync().WaitAsync(TestTimeout).ConfigureAwait(false);

                if (testCaseCatch != null)
                {
                    Assert.Fail($"Expected {testCaseCatch.ErrorKind} exception, but no exception thrown when initializing and starting TelemetryReceiver");
                }

                return telemetryReceiver;
            }
            catch (AkriMqttException exception)
            {
                if (testCaseCatch == null)
                {
                    Assert.Fail($"Unexpected exception thrown initializing or starting TelemetryReceiver: {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);

                return null;
            }
        }

        private async Task ReceiveTelemetryAsync(TestCaseActionReceiveTelemetry actionReceiveTelemetry, StubMqttClient stubMqttClient, ConcurrentDictionary<int, string> sourceIds, ConcurrentDictionary<int, ushort> packetIds, int testCaseIndex)
        {
            string? sourceId = null;
            if (actionReceiveTelemetry.SourceIndex != null)
            {
                if (!sourceIds.TryGetValue((int)actionReceiveTelemetry.SourceIndex, out sourceId))
                {
                    sourceId = Guid.NewGuid().ToString();
                    sourceIds[(int)actionReceiveTelemetry.SourceIndex] = sourceId;
                }
            }

            ushort? specificPacketId = null;
            if (actionReceiveTelemetry.PacketIndex != null)
            {
                if (packetIds.TryGetValue((int)actionReceiveTelemetry.PacketIndex, out ushort extantPacketId))
                {
                    specificPacketId = extantPacketId;
                }
            }

            MqttApplicationMessageBuilder requestAppMsgBuilder = new MqttApplicationMessageBuilder().WithTopic(actionReceiveTelemetry.Topic);

            if (actionReceiveTelemetry.ContentType != null)
            {
                requestAppMsgBuilder.WithContentType(actionReceiveTelemetry.ContentType);
            }

            if (actionReceiveTelemetry.FormatIndicator != null)
            {
                requestAppMsgBuilder.WithPayloadFormatIndicator((MqttPayloadFormatIndicator)(int)actionReceiveTelemetry.FormatIndicator);
            }

            if (actionReceiveTelemetry.Payload != null)
            {
                byte[]? payload =
                    actionReceiveTelemetry.BypassSerialization ? Encoding.UTF8.GetBytes(actionReceiveTelemetry.Payload) :
                    payloadSerializer.ToBytes(actionReceiveTelemetry.Payload).SerializedPayload;
                requestAppMsgBuilder.WithPayload(payload);
            }

            if (sourceId != null)
            {
                requestAppMsgBuilder.WithUserProperty(AkriSystemProperties.SourceId, sourceId);
            }

            if (actionReceiveTelemetry.Qos != null)
            {
                requestAppMsgBuilder.WithQualityOfServiceLevel((MqttQualityOfServiceLevel)actionReceiveTelemetry.Qos);
            }

            if (actionReceiveTelemetry.MessageExpiry != null)
            {
                requestAppMsgBuilder.WithMessageExpiryInterval((uint)actionReceiveTelemetry.MessageExpiry.ToTimeSpan().TotalSeconds);
            }

            foreach (KeyValuePair<string, string> kvp in actionReceiveTelemetry.Metadata)
            {
                requestAppMsgBuilder.WithUserProperty(kvp.Key, kvp.Value);
            }

            MqttApplicationMessage requestAppMsg = requestAppMsgBuilder.Build();

            ushort actualPacketId = await stubMqttClient.ReceiveMessageAsync(requestAppMsg, specificPacketId).WaitAsync(TestTimeout).ConfigureAwait(false);
            if (actionReceiveTelemetry.PacketIndex != null)
            {
                packetIds.TryAdd((int)actionReceiveTelemetry.PacketIndex, actualPacketId);
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

        private void CheckReceivedTelemetry(TestCaseReceivedTelemetry receivedTelemetry, int testCaseIndex, AsyncQueue<ReceivedTelemetry> receivedTelemetries, ConcurrentDictionary<int, string> sourceIds)
        {
            Assert.True(receivedTelemetries.TryDequeue(out ReceivedTelemetry? actualReceivedTelemetry));
            Assert.NotNull(actualReceivedTelemetry);

            if (receivedTelemetry.TelemetryValue == null)
            {
                Assert.Null(actualReceivedTelemetry.TelemetryValue);
            }
            else if (receivedTelemetry.TelemetryValue is string telemetry)
            {
                Assert.Equal(telemetry, actualReceivedTelemetry.TelemetryValue);
            }

            foreach (KeyValuePair<string, string?> kvp in receivedTelemetry.Metadata)
            {
                if (kvp.Value != null)
                {
                    Assert.True(actualReceivedTelemetry.Metadata.TryGetValue(kvp.Key, out string? value));
                    Assert.Equal(kvp.Value, value);
                }
                else
                {
                    Assert.False(actualReceivedTelemetry.Metadata.TryGetValue(kvp.Key, out string? value), $"header {kvp.Key} unexpectedly present with value '{value}'");
                }
            }

            if (receivedTelemetry.CloudEvent != null)
            {
                Assert.NotNull(actualReceivedTelemetry.CloudEvent);

                if (receivedTelemetry.CloudEvent.Source != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.Source, actualReceivedTelemetry.CloudEvent.Source);
                }

                if (receivedTelemetry.CloudEvent.Type != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.Type, actualReceivedTelemetry.CloudEvent.Type);
                }

                if (receivedTelemetry.CloudEvent.SpecVersion != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.SpecVersion, actualReceivedTelemetry.CloudEvent.SpecVersion);
                }

                if (receivedTelemetry.CloudEvent.DataContentType != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.DataContentType, actualReceivedTelemetry.CloudEvent.DataContentType);
                }

                if (receivedTelemetry.CloudEvent.Subject != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.Subject, actualReceivedTelemetry.CloudEvent.Subject);
                }

                if (receivedTelemetry.CloudEvent.DataSchema != null)
                {
                    Assert.Equal(receivedTelemetry.CloudEvent.DataSchema, actualReceivedTelemetry.CloudEvent.DataSchema);
                }
            }

            if (receivedTelemetry.SourceIndex != null)
            {
                Assert.True(sourceIds.TryGetValue((int)receivedTelemetry.SourceIndex, out string? sourceId));
                Assert.Equal(sourceId, actualReceivedTelemetry.SourceId);
            }
        }

        private static void ProcessTelemetry(string sourceId, string telemetry, IncomingTelemetryMetadata metadata,TestCaseReceiver testCaseReceiver, AsyncQueue<ReceivedTelemetry> receivedTelemetries)
        {
            if (testCaseReceiver.RaiseError != null && testCaseReceiver.RaiseError.Kind != TestErrorKind.None)
            {
                throw testCaseReceiver.RaiseError.Kind == TestErrorKind.Content ?
                    new InvocationException(testCaseReceiver.RaiseError.Message, testCaseReceiver.RaiseError.PropertyName, testCaseReceiver.RaiseError.PropertyValue) :
                    new ApplicationException(testCaseReceiver.RaiseError.Message);
            }

            CloudEvent? cloudEvent = null;
            try
            {
                cloudEvent = metadata.GetCloudEvent();
            }
            catch (Exception)
            { 
                // it wasn't a cloud event, ignore this error
            }

            receivedTelemetries.Enqueue(new ReceivedTelemetry(telemetry, metadata.UserData, cloudEvent, sourceId));
        }

        private record ReceivedTelemetry
        {
            public ReceivedTelemetry(string telemetryValue, Dictionary<string, string> metadata, CloudEvent? cloudEvent, string sourceId)
            {
                TelemetryValue = telemetryValue;
                Metadata = metadata;
                SourceId = sourceId;

                if (cloudEvent != null)
                {
                    CloudEvent = new();
                    CloudEvent.Source = cloudEvent.Source?.ToString();
                    CloudEvent.Type = cloudEvent.Type;
                    CloudEvent.SpecVersion = cloudEvent.SpecVersion;
                    CloudEvent.DataContentType = cloudEvent.DataContentType;
                    CloudEvent.Subject = cloudEvent.Subject;
                    CloudEvent.DataSchema = cloudEvent.DataSchema;
                }
            }

            public string TelemetryValue { get; }

            public Dictionary<string, string> Metadata { get; }

            public TestCaseCloudEvent? CloudEvent { get; }

            public string SourceId { get; }
        }
    }
}
