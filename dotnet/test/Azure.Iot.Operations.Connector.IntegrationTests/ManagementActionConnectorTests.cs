// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Azure.Iot.Operations.Mqtt;
using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    /// <summary>
    /// Integration tests for the <c>ManagementActionConnector</c> sample. These
    /// tests assume that <c>deploy-connector-and-device.sh</c> has already brought
    /// up the connector pod and applied the device + asset CRs (the CI workflow
    /// does this exactly the same way as the rest/tcp connector tests).
    /// </summary>
    /// <remarks>
    /// <para>The sample asset (<c>my-mgmt-action-asset</c>) declares a single
    /// management group <c>device-control</c> with three actions, one of each
    /// type (Call / Read / Write). Tests invoke each action via raw MQTT 5 RPC
    /// (publish to the action's topic with <c>ResponseTopic</c> +
    /// <c>CorrelationData</c>, listen on the response topic) and validate the
    /// returned payload + status headers.</para>
    /// <para>These tests are expected to fail until the
    /// <c>maxim/management-action</c> Part 1 internals (currently
    /// <see cref="System.NotImplementedException"/> stubs in <c>AssetClient</c>)
    /// are wired up. They are intentionally <em>not</em> marked
    /// <c>[Fact(Skip = "...")]</c> — they should turn green incrementally as
    /// each piece of the invocation pipeline lands.</para>
    /// </remarks>
    public class ManagementActionConnectorTests
    {
        // Constants in lock-step with KubernetesResources/mgmt-action-asset-definition.yaml.
        private const string DeviceName = "my-mgmt-action-device";
        private const string EndpointName = "my_mgmt_endpoint";
        private const string AssetName = "my-mgmt-action-asset";
        private const string GroupName = "device-control";

        private const string RebootRequestTopic =
            "mgmt/device-1/asset-1/device-control/reboot";
        private const string ReadTemperatureRequestTopic =
            "mgmt/device-1/asset-1/device-control/read-temperature";
        private const string WriteConfigurationRequestTopic =
            "mgmt/device-1/asset-1/device-control/write-configuration";

        private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(15);

        [Fact]
        public async Task Reboot_Call_ReturnsRebootResponse()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();

            var request = new MgmtRebootRequest { Force = true };
            MqttApplicationMessage response = await InvokeAsync(
                mqtt,
                requestTopic: RebootRequestTopic,
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(request));

            AssertSuccess(response);
            MgmtRebootResponse body = DeserializeBody<MgmtRebootResponse>(response);
            Assert.NotEqual(Guid.Empty, body.RequestId);
            Assert.True(body.RebootCount >= 1);
        }

        [Fact]
        public async Task ReadTemperature_Read_ReturnsTemperatureReading()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();

            MqttApplicationMessage response = await InvokeAsync(
                mqtt,
                requestTopic: ReadTemperatureRequestTopic,
                requestPayload: Array.Empty<byte>());

            // The sample's reboot Call action puts the device into a (simulated)
            // ~2s "rebooting" window. If the test order happens to interleave a
            // read with that window, a DeviceUnavailable application error is a
            // legitimate response — assert one of the two outcomes instead of
            // racing.
            int status = GetStatus(response);
            if (status is 200 or 204)
            {
                MgmtTemperatureReading body = DeserializeBody<MgmtTemperatureReading>(response);
                Assert.True(body.Value > -100 && body.Value < 200, $"unexpected temperature {body.Value}");
                Assert.True(body.Unit is "C" or "F");
            }
            else
            {
                AssertApplicationError(response, expectedCode: "DeviceUnavailable");
            }
        }

        [Fact]
        public async Task WriteConfiguration_Write_ReturnsAckAndAffectsSubsequentRead()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();

            // 1. Apply a known configuration. Use 'F' so it's distinguishable from the default ('C').
            var update = new MgmtConfigurationUpdate { SampleIntervalMs = 750, Unit = "F" };
            MqttApplicationMessage writeResponse = await InvokeAsync(
                mqtt,
                requestTopic: WriteConfigurationRequestTopic,
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(update));

            AssertSuccess(writeResponse);
            MgmtConfigurationAck ack = DeserializeBody<MgmtConfigurationAck>(writeResponse);
            Assert.Equal(750, ack.AppliedSampleIntervalMs);
            Assert.Equal("F", ack.AppliedUnit);

            // 2. Read back — the sample's FakeDevice is a singleton, so the unit
            //    must reflect the write we just made (modulo a possible "rebooting"
            //    window from the Reboot test running first; retry briefly).
            await WaitForReadAsync(mqtt, expectedUnit: "F");
        }

        [Fact]
        public async Task WriteConfiguration_InvalidPayload_ReturnsValidationFailedApplicationError()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();

            // sampleIntervalMs out of range (must be 100..60000 per the sample).
            var bad = new MgmtConfigurationUpdate { SampleIntervalMs = 50, Unit = "C" };
            MqttApplicationMessage response = await InvokeAsync(
                mqtt,
                requestTopic: WriteConfigurationRequestTopic,
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(bad));

            AssertApplicationError(response, expectedCode: "ValidationFailed");
        }

        [Fact]
        public async Task AssetStatusReportsAllThreeActions()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using AzureDeviceRegistryClient adrClient = new(new(), mqtt);

            int retry = 0;
            while (true)
            {
                try
                {
                    DeviceStatus deviceStatus = await adrClient.GetDeviceStatusAsync(DeviceName, EndpointName);
                    Assert.NotNull(deviceStatus.Config);
                    Assert.Null(deviceStatus.Config.Error);

                    AssetStatus assetStatus = await adrClient.GetAssetStatusAsync(DeviceName, EndpointName, AssetName);
                    Assert.NotNull(assetStatus.Config);
                    Assert.Null(assetStatus.Config.Error);
                    Assert.NotNull(assetStatus.ManagementGroups);

                    var group = Assert.Single(assetStatus.ManagementGroups);
                    Assert.Equal(GroupName, group.Name);
                    Assert.NotNull(group.Actions);
                    Assert.Equal(3, group.Actions!.Count);

                    var actionNames = new HashSet<string>(group.Actions.Select(a => a.Name));
                    Assert.Contains("reboot", actionNames);
                    Assert.Contains("read-temperature", actionNames);
                    Assert.Contains("write-configuration", actionNames);
                    foreach (var action in group.Actions)
                    {
                        Assert.Null(action.Error);
                    }
                    return;
                }
                catch
                {
                    if (++retry > 5) throw;
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static async Task WaitForReadAsync(OrderedAckMqttClient mqtt, string expectedUnit)
        {
            for (int i = 0; i < 20; i++)
            {
                MqttApplicationMessage response = await InvokeAsync(
                    mqtt,
                    requestTopic: ReadTemperatureRequestTopic,
                    requestPayload: Array.Empty<byte>());

                int status = GetStatus(response);
                if (status is 200 or 204)
                {
                    var body = DeserializeBody<MgmtTemperatureReading>(response);
                    if (string.Equals(body.Unit, expectedUnit, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Otherwise: device is rebooting or hasn't observed the write yet. Retry briefly.
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.Fail($"ReadTemperature never returned unit '{expectedUnit}' after WriteConfiguration.");
        }

        /// <summary>
        /// Performs an MQTT 5 RPC call: subscribes to a unique response topic,
        /// publishes the request with <c>ResponseTopic</c> + <c>CorrelationData</c>,
        /// and returns the matching response message.
        /// </summary>
        private static async Task<MqttApplicationMessage> InvokeAsync(
            OrderedAckMqttClient mqtt,
            string requestTopic,
            byte[] requestPayload)
        {
            byte[] correlationData = Guid.NewGuid().ToByteArray();
            string responseTopic = $"clients/{mqtt.ClientId}/mgmt-test/resp/{Guid.NewGuid():N}";

            var tcs = new TaskCompletionSource<MqttApplicationMessage>();
            Task Handler(MqttApplicationMessageReceivedEventArgs args)
            {
                if (args.ApplicationMessage.Topic == responseTopic
                    && args.ApplicationMessage.CorrelationData is { } corr
                    && corr.AsSpan().SequenceEqual(correlationData))
                {
                    tcs.TrySetResult(args.ApplicationMessage);
                }
                return Task.CompletedTask;
            }

            mqtt.ApplicationMessageReceivedAsync += Handler;
            try
            {
                await mqtt.SubscribeAsync(new MqttClientSubscribeOptions
                {
                    TopicFilters = { new MqttTopicFilter(responseTopic) }
                });

                var pub = new MqttApplicationMessage(requestTopic)
                {
                    PayloadSegment = requestPayload,
                    CorrelationData = correlationData,
                    ResponseTopic = responseTopic,
                    ContentType = "application/json",
                    MessageExpiryInterval = (uint)ResponseWait.TotalSeconds + 5,
                };

                await mqtt.PublishAsync(pub);

                return await tcs.Task.WaitAsync(ResponseWait);
            }
            finally
            {
                mqtt.ApplicationMessageReceivedAsync -= Handler;
                try
                {
                    await mqtt.UnsubscribeAsync(new MqttClientUnsubscribeOptions
                    {
                        TopicFilters = { responseTopic }
                    });
                }
                catch
                {
                    // Best-effort cleanup; not critical for test correctness.
                }
            }
        }

        private static int GetStatus(MqttApplicationMessage response)
        {
            string? statusValue = response.UserProperties?
                .FirstOrDefault(p => p.Name == "__stat")?.Value;
            Assert.False(string.IsNullOrEmpty(statusValue), "response is missing the __stat user property");
            return int.Parse(statusValue!, CultureInfo.InvariantCulture);
        }

        private static void AssertSuccess(MqttApplicationMessage response)
        {
            int status = GetStatus(response);
            string? statusMessage = response.UserProperties?.FirstOrDefault(p => p.Name == "__stMsg")?.Value;
            Assert.True(
                status is 200 or 204,
                $"expected 200/204 but got {status}: {statusMessage}");
        }

        private static void AssertApplicationError(MqttApplicationMessage response, string expectedCode)
        {
            int status = GetStatus(response);
            Assert.NotEqual(200, status);
            Assert.NotEqual(204, status);

            string? isAppError = response.UserProperties?.FirstOrDefault(p => p.Name == "__apErr")?.Value;
            Assert.Equal("true", isAppError, ignoreCase: true);

            string? appErrCode = response.UserProperties?.FirstOrDefault(p => p.Name == "AppErrCode")?.Value;
            Assert.Equal(expectedCode, appErrCode);
        }

        private static T DeserializeBody<T>(MqttApplicationMessage response)
        {
            ReadOnlySequence<byte> payload = response.Payload;
            Assert.False(payload.IsEmpty, "expected a JSON body but the response payload is empty");
            T? value = JsonSerializer.Deserialize<T>(payload.ToArray());
            Assert.NotNull(value);
            return value!;
        }
    }
}
