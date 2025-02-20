// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Converters;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Telemetry;
using Microsoft.VisualStudio.Threading;
using Tomlyn;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TelemetrySenderTester
    {
        private const string testCasesPath = "../../../../../../eng/test/test-cases";
        private const string senderCasesPath = $"{testCasesPath}/Protocol/TelemetrySender";
        private const string defaultsFilePath = $"{testCasesPath}/Protocol/TelemetrySender/defaults.toml";

        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

        private static readonly HashSet<string> problematicTestCases = new HashSet<string>
        {
            "TelemetrySenderSendWithCloudEventDataContentTypeNonConforming_ThrowsException",
            "TelemetrySenderSendWithCloudEventDataSchemaEmpty_ThrowsException",
            "TelemetrySenderSendWithCloudEventDataSchemaNonUri_ThrowsException",
            "TelemetrySenderSendWithCloudEventDataSchemaRelativeUri_ThrowsException",
            "TelemetrySenderSendWithCloudEventIdEmpty_ThrowsException",
            "TelemetrySenderSendWithCloudEventSourceNonUri_ThrowsException",
            "TelemetrySenderSendWithCloudEventSpecVersionEmpty_ThrowsException",
            "TelemetrySenderSendWithCloudEventSubjectEmpty_ThrowsException",
            "TelemetrySenderSendWithCloudEventTypeEmpty_ThrowsException",
        };

        private static IDeserializer yamlDeserializer;
        private static AsyncAtomicInt TestCaseIndex = new(0);

        static TelemetrySenderTester()
        {
            yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
                .WithTypeDiscriminatingNodeDeserializer(options =>
                {
                    options.AddKeyValueTypeDiscriminator<TestCaseAction>("action",
                        ("send telemetry", typeof(TestCaseActionSendTelemetry)),
                        ("await send", typeof(TestCaseActionAwaitSend)),
                        ("await publish", typeof(TestCaseActionAwaitPublish)),
                        ("disconnect", typeof(TestCaseActionDisconnect)));
                })
                .Build();

            if (File.Exists(defaultsFilePath))
            {
                DefaultTestCase defaultTestCase = Toml.ToModel<DefaultTestCase>(File.ReadAllText(defaultsFilePath), defaultsFilePath, new TomlModelOptions { ConvertPropertyName = CaseConverter.PascalToKebabCase });

                TestCaseSerializer.DefaultOutContentType = defaultTestCase.Prologue.Sender.Serializer.OutContentType;
                TestCaseSerializer.DefaultIndicateCharacterData = defaultTestCase.Prologue.Sender.Serializer.IndicateCharacterData;

                TestCaseSender.DefaultTelemetryName = defaultTestCase.Prologue.Sender.TelemetryName;
                TestCaseSender.DefaultTelemetryTopic = defaultTestCase.Prologue.Sender.TelemetryTopic;
                TestCaseSender.DefaultTopicNamespace = defaultTestCase.Prologue.Sender.TopicNamespace;

                TestCaseActionSendTelemetry.DefaultTelemetryName = defaultTestCase.Actions.SendTelemetry.TelemetryName;
                TestCaseActionSendTelemetry.DefaultTelemetryValue = defaultTestCase.Actions.SendTelemetry.TelemetryValue;
                TestCaseActionSendTelemetry.DefaultTimeout = defaultTestCase.Actions.SendTelemetry.Timeout;
                TestCaseActionSendTelemetry.DefaultQos = defaultTestCase.Actions.SendTelemetry.Qos;
            }
        }

        public static IEnumerable<object[]> GetAllTelemetrySenderCases()
        {
            foreach (string testCasePath in Directory.GetFiles(senderCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{senderCasesPath}/{testCaseName}.yaml"))
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

        public static IEnumerable<object[]> GetRestrictedTelemetrySenderCases()
        {
            foreach (string testCasePath in Directory.GetFiles(senderCasesPath, @"*.yaml"))
            {
                string testCaseName = Path.GetFileNameWithoutExtension(testCasePath);
                using (StreamReader streamReader = File.OpenText($"{senderCasesPath}/{testCaseName}.yaml"))
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
        [MemberData(nameof(GetAllTelemetrySenderCases))]
        public Task TestTelemetrySenderWithSessionClient(string testCaseName)
        {
            return TestTelemetrySenderProtocol(testCaseName, includeSessionClient: true);
        }

        [Theory]
        [MemberData(nameof(GetRestrictedTelemetrySenderCases))]
        public Task TestTelemetrySenderStandalone(string testCaseName)
        {
            return TestTelemetrySenderProtocol(testCaseName, includeSessionClient: false);
        }

        private async Task TestTelemetrySenderProtocol(string testCaseName, bool includeSessionClient)
        {
            int testCaseIndex = await TestCaseIndex.Increment().ConfigureAwait(false);

            TestCase testCase;
            using (StreamReader streamReader = File.OpenText($"{senderCasesPath}/{testCaseName}.yaml"))
            {
                testCase = yamlDeserializer.Deserialize<TestCase>(new Parser(streamReader));
            }

            Dictionary<string, TestTelemetrySender> telemetrySenders = new();

            string clientIdPrefix = includeSessionClient ? "Session" : "Standalone";
            string mqttClientId = testCase.Prologue?.MqttConfig?.ClientId ?? $"{clientIdPrefix}SenderTestClient{testCaseIndex}";
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

            foreach (TestCaseSender testCaseSender in testCase.Prologue?.Senders ?? new List<TestCaseSender>())
            {
                bool isLast = ReferenceEquals(testCaseSender, testCase.Prologue?.Senders.Last());
                TestTelemetrySender? telemetrySender = await GetTelemetrySender(compositeMqttClient, testCaseSender, isLast ? testCase.Prologue?.Catch : null);
                if (telemetrySender == null)
                {
                    return;
                }

                telemetrySenders[testCaseSender.TelemetryName!] = telemetrySender;
            }

            AsyncQueue<Task> sendTasks = new();

            foreach (TestCaseAction action in testCase.Actions)
            {
                switch (action)
                {
                    case TestCaseActionSendTelemetry actionSendTelemetry:
                        SendTelemetryAsync(actionSendTelemetry, telemetrySenders, sendTasks);
                        break;
                    case TestCaseActionAwaitSend actionAwaitSend:
                        await AwaitSendAsync(actionAwaitSend, sendTasks).ConfigureAwait(false);
                        break;
                    case TestCaseActionAwaitPublish:
                        await AwaitPublishAsync(stubMqttClient).ConfigureAwait(false);
                        break;
                    case TestCaseActionDisconnect:
                        await DisconnectAsync(stubMqttClient).ConfigureAwait(false);
                        break;
                }
            }

            while (sendTasks.TryDequeue(out Task? sendTask))
            {
                try
                {
                    await sendTask.WaitAsync(TestTimeout).ConfigureAwait(false);
                }
                catch (AkriMqttException exception)
                {
                    Assert.Fail($"Unexpected exception thrown when awaiting TelemetrySender.SendTelemetryAsync(): {exception.Message}");
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

                int sequenceIndex = 0;
                foreach (TestCasePublishedMessage publishedMessage in testCase.Epilogue.PublishedMessages)
                {
                    CheckPublishedMessage(sequenceIndex, publishedMessage, stubMqttClient);
                    sequenceIndex++;
                }

                if (testCase.Epilogue.AcknowledgementCount != null)
                {
                    int acknowledgementCount = await stubMqttClient.GetAcknowledgementCount().ConfigureAwait(false);
                    Assert.Equal(testCase.Epilogue.AcknowledgementCount, acknowledgementCount);
                }
            }
        }

        private async Task<TestTelemetrySender?> GetTelemetrySender(IMqttPubSubClient mqttClient, TestCaseSender testCaseSender, TestCaseCatch? testCaseCatch)
        {
            try
            {
                TestSerializer testSerializer = new TestSerializer(testCaseSender.Serializer);

                TestTelemetrySender telemetrySender = new TestTelemetrySender(new ApplicationContext(), mqttClient, testCaseSender.TelemetryName!, testSerializer)
                {
                    TopicPattern = testCaseSender.TelemetryTopic!,
                    TopicNamespace = testCaseSender.TopicNamespace,
                };

                if (testCaseSender.TopicTokenMap != null)
                {
                    foreach (KeyValuePair<string, string> kvp in testCaseSender.TopicTokenMap)
                    {
                        telemetrySender.TopicTokenMap![kvp.Key] = kvp.Value;
                    }
                }

                if (testCaseCatch != null)
                {
                    // TelemetrySender has no Start method, so if an exception is expected, Send may be needed to trigger it.
                    try
                    {
                        await telemetrySender.SendTelemetryAsync(TestCaseActionSendTelemetry.DefaultTelemetryValue!).WaitAsync(TestTimeout);
                    }
                    catch (AkriMqttException exception)
                    {
                        if (exception.Kind != AkriMqttErrorKind.Cancellation)
                        {
                            AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);
                            return null;
                        }
                    }

                    Assert.Fail($"Expected {testCaseCatch.ErrorKind} exception, but no exception thrown when initializing TelemetrySender");
                }

                return telemetrySender;
            }
            catch (AkriMqttException exception)
            {
                if (testCaseCatch == null)
                {
                    Assert.Fail($"Unexpected exception thrown initializing TelemetrySender: {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(testCaseCatch, exception);
                return null;
            }
        }

        private void SendTelemetryAsync(TestCaseActionSendTelemetry actionSendTelemetry, Dictionary<string, TestTelemetrySender> telemetrySenders, AsyncQueue<Task> sendTasks)
        {
            OutgoingTelemetryMetadata metadata = new OutgoingTelemetryMetadata();

            if (actionSendTelemetry.Metadata != null)
            {
                foreach (KeyValuePair<string, string> kvp in actionSendTelemetry.Metadata)
                {
                    metadata.UserData[kvp.Key] = kvp.Value;
                }
            }

            if (actionSendTelemetry.CloudEvent != null)
            {
                Uri sourceUri = new Uri(actionSendTelemetry.CloudEvent.Source!, UriKind.RelativeOrAbsolute);

                if (actionSendTelemetry.CloudEvent.Type != null && actionSendTelemetry.CloudEvent.SpecVersion != null)
                {
                    metadata.CloudEvent = new CloudEvent(sourceUri, actionSendTelemetry.CloudEvent.Type, actionSendTelemetry.CloudEvent.SpecVersion);
                }
                else if (actionSendTelemetry.CloudEvent.Type != null)
                {
                    metadata.CloudEvent = new CloudEvent(sourceUri, type: actionSendTelemetry.CloudEvent.Type);
                }
                else if (actionSendTelemetry.CloudEvent.SpecVersion != null)
                {
                    metadata.CloudEvent = new CloudEvent(sourceUri, specversion: actionSendTelemetry.CloudEvent.SpecVersion);
                }
                else
                {
                    metadata.CloudEvent = new CloudEvent(sourceUri);
                }

                if (actionSendTelemetry.CloudEvent.Id != null)
                {
                    metadata.CloudEvent.Id = actionSendTelemetry.CloudEvent.Id;
                }

                if (actionSendTelemetry.CloudEvent.Time is string timeString)
                {
                    if (DateTime.TryParse(timeString, null, DateTimeStyles.RoundtripKind, out DateTime time))
                    {
                        metadata.CloudEvent.Time = time;
                    }
                    else
                    {
                        sendTasks.Enqueue(Task.FromException(AkriMqttException.GetArgumentInvalidException(null, "CloudEvent", null, null)));
                        return;
                    }
                }

                if (actionSendTelemetry.CloudEvent.Subject is string subject)
                {
                    metadata.CloudEvent.Subject = subject;
                }

                if (actionSendTelemetry.CloudEvent.DataSchema is string dataSchema)
                {
                    metadata.CloudEvent.DataSchema = dataSchema;
                }
            }

            MqttQualityOfServiceLevel qos = actionSendTelemetry.Qos != null ? (MqttQualityOfServiceLevel)actionSendTelemetry.Qos : MqttQualityOfServiceLevel.AtLeastOnce;
            sendTasks.Enqueue(telemetrySenders[actionSendTelemetry.TelemetryName!].SendTelemetryAsync(actionSendTelemetry.TelemetryValue!, metadata, qos, actionSendTelemetry.Timeout?.ToTimeSpan()));
        }

        private async Task AwaitSendAsync(TestCaseActionAwaitSend actionAwaitSend, AsyncQueue<Task> sendTasks)
        {
            try
            {
                Task sendTask = await sendTasks.DequeueAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
                await sendTask.WaitAsync(TestTimeout).ConfigureAwait(false);

                if (actionAwaitSend.Catch != null)
                {
                    Assert.Fail($"Expected {actionAwaitSend.Catch.ErrorKind} exception, but no exception thrown when awaiting TelemetrySender.SendTelemetryAsync()");
                }
            }
            catch (AkriMqttException exception)
            {
                if (actionAwaitSend.Catch == null)
                {
                    Assert.Fail($"Unexpected exception thrown when awaiting TelemetrySender.SendTelemetryAsync(): {exception.Message}");
                }

                AkriMqttExceptionChecker.CheckException(actionAwaitSend.Catch, exception);
            }
        }

        private async Task AwaitPublishAsync(StubMqttClient stubMqttClient)
        {
            await stubMqttClient.AwaitPublishAsync().WaitAsync(TestTimeout).ConfigureAwait(false);
        }

        private Task DisconnectAsync(StubMqttClient stubMqttClient)
        {
            return stubMqttClient.DisconnectAsync(new MQTTnet.Client.MqttClientDisconnectOptions());
        }

        private void CheckPublishedMessage(int sequenceIndex, TestCasePublishedMessage publishedMessage, StubMqttClient stubMqttClient)
        {
            MQTTnet.MqttApplicationMessage? appMsg = stubMqttClient.GetPublishedMessage(sequenceIndex);
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
                Assert.NotNull(appMsg.PayloadSegment.Array);
                Assert.Equal(payload, Encoding.UTF8.GetString(appMsg.PayloadSegment.Array));
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
                    Assert.True(MqttNetConverter.ToGeneric(appMsg.UserProperties).TryGetProperty(kvp.Key, out string? value), $"header {kvp.Key} not present");
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
