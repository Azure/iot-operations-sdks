// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Mqtt;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.RPC;
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
    /// type (Call / Read / Write). Tests invoke each action via a real
    /// <see cref="CommandInvoker{TReq, TResp}"/> — the same RPC primitive a
    /// downstream service would use — and validate the deserialized
    /// response body. Non-2xx responses surface as
    /// <see cref="AkriMqttException"/> with <see cref="AkriMqttErrorKind.ExecutionException"/>
    /// for application errors, which the error-path tests assert against.</para>
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

        private static readonly TimeSpan ResponseWait = TimeSpan.FromSeconds(15);

        [Fact]
        public async Task Reboot_Call_ReturnsRebootResponse()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using var invoker = CreateInvoker(mqtt);

            var request = new MgmtRebootRequest { Force = true };
            byte[] responseBytes = await InvokeAsync(
                invoker,
                actionName: "reboot",
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(request));

            MgmtRebootResponse body = DeserializeBody<MgmtRebootResponse>(responseBytes);
            Assert.NotEqual(Guid.Empty, body.RequestId);
            Assert.True(body.RebootCount >= 1);
        }

        [Fact]
        public async Task ReadTemperature_Read_ReturnsTemperatureReading()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using var invoker = CreateInvoker(mqtt);

            try
            {
                byte[] responseBytes = await InvokeAsync(
                    invoker,
                    actionName: "read-temperature",
                    requestPayload: Array.Empty<byte>());

                MgmtTemperatureReading body = DeserializeBody<MgmtTemperatureReading>(responseBytes);
                Assert.True(body.Value > -100 && body.Value < 200, $"unexpected temperature {body.Value}");
                Assert.True(body.Unit is "C" or "F");
            }
            catch (AkriMqttException ex) when (IsApplicationError(ex))
            {
                // The sample's reboot Call action puts the FakeDevice into a
                // (simulated) ~2s "rebooting" window. If the test order happens
                // to interleave a read with that window, the handler returns a
                // DeviceUnavailable application error (ErrorPayload = "Device is
                // currently rebooting."), which CommandInvoker surfaces as an
                // AkriMqttException with Kind = ExecutionException.
                Assert.Contains("rebooting", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task WriteConfiguration_Write_ReturnsAckAndAffectsSubsequentRead()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using var invoker = CreateInvoker(mqtt);

            // 1. Apply a known configuration. Use 'F' so it's distinguishable from the default ('C').
            var update = new MgmtConfigurationUpdate { SampleIntervalMs = 750, Unit = "F" };
            byte[] writeBytes = await InvokeAsync(
                invoker,
                actionName: "write-configuration",
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(update));

            MgmtConfigurationAck ack = DeserializeBody<MgmtConfigurationAck>(writeBytes);
            Assert.Equal(750, ack.AppliedSampleIntervalMs);
            Assert.Equal("F", ack.AppliedUnit);

            // 2. Read back — the sample's FakeDevice is a singleton, so the unit
            //    must reflect the write we just made (modulo a possible "rebooting"
            //    window from the Reboot test running first; retry briefly).
            await WaitForReadAsync(invoker, expectedUnit: "F");
        }

        [Fact]
        public async Task WriteConfiguration_InvalidPayload_ReturnsValidationFailedApplicationError()
        {
            await using var mqtt = await ClientFactory.CreateSessionClientFromEnvAsync();
            await using var invoker = CreateInvoker(mqtt);

            // sampleIntervalMs out of range (must be 100..60000 per the sample).
            var bad = new MgmtConfigurationUpdate { SampleIntervalMs = 50, Unit = "C" };

            AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(() => InvokeAsync(
                invoker,
                actionName: "write-configuration",
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(bad)));

            // The handler returns ManagementActionApplicationError("ValidationFailed", ...).
            // The connector marshals that to a non-2xx response with the __apErr user
            // property set; CommandInvoker translates that to Kind = ExecutionException.
            Assert.True(
                IsApplicationError(ex),
                $"expected an application error but got Kind={ex.Kind}, IsRemote={ex.IsRemote}: {ex.Message}");
            // The validation handler embeds 'sampleIntervalMs' in its error payload,
            // which the connector forwards as the response __stMsg header (surfaced
            // here as the exception Message). Note: the structured AppErrCode
            // ('ValidationFailed') is not currently exposed through AkriMqttException
            // / ExtendedResponse for non-2xx responses, hence the substring match.
            Assert.Contains("sampleIntervalMs", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        private static async Task WaitForReadAsync(ManagementActionInvoker invoker, string expectedUnit)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    byte[] responseBytes = await InvokeAsync(
                        invoker,
                        actionName: "read-temperature",
                        requestPayload: Array.Empty<byte>());

                    var body = DeserializeBody<MgmtTemperatureReading>(responseBytes);
                    if (string.Equals(body.Unit, expectedUnit, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                catch (AkriMqttException ex) when (IsApplicationError(ex))
                {
                    // Device is rebooting or otherwise refused the read; retry briefly.
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.Fail($"ReadTemperature never returned unit '{expectedUnit}' after WriteConfiguration.");
        }

        /// <summary>
        /// Performs an MQTT 5 RPC call via a real <see cref="CommandInvoker{TReq, TResp}"/>:
        /// the invoker manages response-topic subscription, correlation, status
        /// validation, and exception mapping (non-2xx → <see cref="AkriMqttException"/>).
        /// </summary>
        private static async Task<byte[]> InvokeAsync(
            ManagementActionInvoker invoker,
            string actionName,
            byte[] requestPayload)
        {
            ExtendedResponse<byte[]> response = await invoker.InvokeCommandAsync(
                requestPayload,
                additionalTopicTokenMap: new Dictionary<string, string> { ["actionName"] = actionName },
                commandTimeout: ResponseWait);
            return response.Response;
        }

        private static ManagementActionInvoker CreateInvoker(OrderedAckMqttClient mqtt)
            => new(new ApplicationContext(), mqtt);

        private static bool IsApplicationError(AkriMqttException ex)
            => ex.Kind == AkriMqttErrorKind.ExecutionException && ex.IsRemote;

        private static T DeserializeBody<T>(byte[] payload)
        {
            Assert.NotEmpty(payload);
            T? value = JsonSerializer.Deserialize<T>(payload);
            Assert.NotNull(value);
            return value!;
        }

        // ------------------------------------------------------------------
        // Invoker wiring
        // ------------------------------------------------------------------

        /// <summary>
        /// <see cref="CommandInvoker{TReq, TResp}"/> bound to the sample's
        /// management-action topic shape. The {actionName} token is filled per
        /// invocation via <c>additionalTopicTokenMap</c>, so a single invoker
        /// instance handles all three actions on the asset.
        /// </summary>
        /// <remarks>
        /// The literal device/asset/group segments mirror the asset CR shipped
        /// with the sample (see <c>KubernetesResources/mgmt-action-asset-definition.yaml</c>).
        /// The default response topic is <c>clients/{invokerClientId}/{request topic}</c>,
        /// which is generated automatically by the base class.
        /// </remarks>
        private sealed class ManagementActionInvoker : CommandInvoker<byte[], byte[]>
        {
            public ManagementActionInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, commandName: "managementAction", new RawJsonSerializer())
            {
                RequestTopicPattern = "mgmt/device-1/asset-1/device-control/{actionName}";
            }
        }

        /// <summary>
        /// Passthrough <see cref="IPayloadSerializer"/> that hands raw bytes
        /// to/from the executor and reports <c>application/json</c> as the
        /// content type on outgoing requests.
        /// </summary>
        private sealed class RawJsonSerializer : IPayloadSerializer
        {
            public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
                where T : class
            {
                if (typeof(T) != typeof(byte[]))
                {
                    throw new InvalidOperationException(
                        $"{nameof(RawJsonSerializer)} only supports byte[]; got {typeof(T)}.");
                }
                object bytes = payload.IsEmpty ? Array.Empty<byte>() : payload.ToArray();
                return (T)bytes;
            }

            public SerializedPayloadContext ToBytes<T>(T? payload)
                where T : class
            {
                byte[] bytes = payload as byte[] ?? Array.Empty<byte>();
                return new SerializedPayloadContext(
                    new ReadOnlySequence<byte>(bytes),
                    contentType: "application/json",
                    payloadFormatIndicator: MqttPayloadFormatIndicator.CharacterData);
            }
        }
    }
}
