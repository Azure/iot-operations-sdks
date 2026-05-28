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
using Xunit.Abstractions;

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

        private readonly ITestOutputHelper _output;

        public ManagementActionConnectorTests(ITestOutputHelper output)
        {
            _output = output;
        }

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
            _output.WriteLine($"[Reboot_Call] parsed body: RequestId={body.RequestId}, RebootCount={body.RebootCount}");
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
                _output.WriteLine($"[ReadTemperature_Read] parsed body: Value={body.Value}, Unit={body.Unit}");
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
                _output.WriteLine($"[ReadTemperature_Read] swallowed application error: {DescribeException(ex)}");
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
            _output.WriteLine($"[WriteConfiguration_Write] parsed ack: AppliedSampleIntervalMs={ack.AppliedSampleIntervalMs}, AppliedUnit={ack.AppliedUnit}");
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
            _output.WriteLine($"[WriteConfiguration_InvalidPayload] sending bad payload: {JsonSerializer.Serialize(bad)}");

            // Application errors raised via ExtendedResponse.WithApplicationError are surfaced
            // as a *successful* RPC response carrying the AppErrCode / AppErrPayload user
            // properties (see CounterEnvoyTests for the canonical pattern). CommandInvoker
            // therefore does NOT raise AkriMqttException for these — the caller must inspect
            // ExtendedResponse.TryGetApplicationError().
            ExtendedResponse<byte[]> response = await InvokeExtendedAsync(
                invoker,
                actionName: "write-configuration",
                requestPayload: JsonSerializer.SerializeToUtf8Bytes(bad));

            Assert.True(
                response.IsApplicationError(),
                $"expected application error metadata on response; metadata={DescribeMetadata(response.ResponseMetadata)}");

            Assert.True(
                response.TryGetApplicationError(out string? errorCode, out string? errorPayload),
                "TryGetApplicationError should report true when IsApplicationError() is true");

            // The sample's write-configuration handler emits ManagementActionApplicationError(
            //   "ValidationFailed", "sampleIntervalMs must be between 100 and 60000; got <n>.").
            Assert.Equal("ValidationFailed", errorCode);
            Assert.NotNull(errorPayload);
            Assert.Contains("sampleIntervalMs", errorPayload!, StringComparison.OrdinalIgnoreCase);
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
                    _output.WriteLine($"[AssetStatus] attempt #{retry + 1}: fetching device + asset status for device='{DeviceName}', endpoint='{EndpointName}', asset='{AssetName}'");

                    DeviceStatus deviceStatus = await adrClient.GetDeviceStatusAsync(DeviceName, EndpointName);
                    _output.WriteLine($"[AssetStatus] DeviceStatus: {SafeSerialize(deviceStatus)}");
                    Assert.NotNull(deviceStatus.Config);
                    Assert.Null(deviceStatus.Config.Error);

                    AssetStatus assetStatus = await adrClient.GetAssetStatusAsync(DeviceName, EndpointName, AssetName);
                    _output.WriteLine($"[AssetStatus] AssetStatus: {SafeSerialize(assetStatus)}");
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
                catch (Exception ex)
                {
                    _output.WriteLine($"[AssetStatus] attempt #{retry + 1} failed: {ex.GetType().Name}: {ex.Message}");
                    if (++retry > 5) throw;
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private async Task WaitForReadAsync(ManagementActionInvoker invoker, string expectedUnit)
        {
            for (int i = 0; i < 20; i++)
            {
                _output.WriteLine($"[WaitForRead] iteration {i + 1}/20, expecting unit='{expectedUnit}'");
                try
                {
                    byte[] responseBytes = await InvokeAsync(
                        invoker,
                        actionName: "read-temperature",
                        requestPayload: Array.Empty<byte>());

                    if (responseBytes.Length == 0)
                    {
                        _output.WriteLine($"[WaitForRead] iteration {i + 1}: response payload was empty; retrying");
                    }
                    else
                    {
                        var body = DeserializeBody<MgmtTemperatureReading>(responseBytes);
                        _output.WriteLine($"[WaitForRead] iteration {i + 1}: got Value={body.Value}, Unit='{body.Unit}'");
                        if (string.Equals(body.Unit, expectedUnit, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }
                }
                catch (AkriMqttException ex) when (IsApplicationError(ex))
                {
                    // Device is rebooting or otherwise refused the read; retry briefly.
                    _output.WriteLine($"[WaitForRead] iteration {i + 1}: application error (will retry): {DescribeException(ex)}");
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
        private async Task<byte[]> InvokeAsync(
            ManagementActionInvoker invoker,
            string actionName,
            byte[] requestPayload)
        {
            ExtendedResponse<byte[]> response = await InvokeExtendedAsync(invoker, actionName, requestPayload);
            return response.Response ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Variant of <see cref="InvokeAsync"/> that returns the full <see cref="ExtendedResponse{TResp}"/>
        /// so the caller can inspect response metadata (e.g. <c>AppErrCode</c> / <c>AppErrPayload</c>
        /// user properties via <see cref="ExtendedResponse{TResp}.TryGetApplicationError"/>).
        /// </summary>
        private async Task<ExtendedResponse<byte[]>> InvokeExtendedAsync(
            ManagementActionInvoker invoker,
            string actionName,
            byte[] requestPayload)
        {
            _output.WriteLine(
                $"[Invoke] action='{actionName}', requestBytes={requestPayload.Length}, request UTF-8='{SafeUtf8(requestPayload)}'");
            try
            {
                ExtendedResponse<byte[]> response = await invoker.InvokeCommandAsync(
                    requestPayload,
                    additionalTopicTokenMap: new Dictionary<string, string> { ["actionName"] = actionName },
                    commandTimeout: ResponseWait);

                byte[] payload = response.Response ?? Array.Empty<byte>();
                _output.WriteLine(
                    $"[Invoke] action='{actionName}' got responseBytes={payload.Length}, response UTF-8='{SafeUtf8(payload)}'");
                _output.WriteLine(
                    $"[Invoke] action='{actionName}' response metadata: {DescribeMetadata(response.ResponseMetadata)}");
                return response;
            }
            catch (AkriMqttException ex)
            {
                _output.WriteLine($"[Invoke] action='{actionName}' threw: {DescribeException(ex)}");
                throw;
            }
        }

        private ManagementActionInvoker CreateInvoker(OrderedAckMqttClient mqtt)
        {
            var inv = new ManagementActionInvoker(new ApplicationContext(), mqtt);
            _output.WriteLine($"[CreateInvoker] clientId='{mqtt.ClientId}'");
            return inv;
        }

        private static bool IsApplicationError(AkriMqttException ex)
            => ex.Kind == AkriMqttErrorKind.ExecutionException && ex.IsRemote;

        private T DeserializeBody<T>(byte[] payload)
        {
            _output.WriteLine(
                $"[Deserialize<{typeof(T).Name}>] bytes={payload.Length}, hex={BitConverter.ToString(payload)}, UTF-8='{SafeUtf8(payload)}'");
            Assert.NotEmpty(payload);
            T? value = JsonSerializer.Deserialize<T>(payload);
            Assert.NotNull(value);
            return value!;
        }

        private static string SafeUtf8(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return string.Empty;
            try { return System.Text.Encoding.UTF8.GetString(payload); }
            catch (Exception ex) { return $"<utf8 decode failed: {ex.Message}>"; }
        }

        private static string SafeSerialize(object? value)
        {
            if (value == null) return "<null>";
            try { return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false }); }
            catch (Exception ex) { return $"<{value.GetType().Name}: serialize failed: {ex.Message}>"; }
        }

        private static string DescribeException(AkriMqttException ex)
        {
            return $"Kind={ex.Kind}, IsRemote={ex.IsRemote}, IsShallow={ex.IsShallow}, " +
                   $"CorrelationId={ex.CorrelationId?.ToString() ?? "<null>"}, " +
                   $"HeaderName='{ex.HeaderName}', HeaderValue='{ex.HeaderValue}', " +
                   $"PropertyName='{ex.PropertyName}', PropertyValue='{ex.PropertyValue}', " +
                   $"CommandName='{ex.CommandName}', Message='{ex.Message}'";
        }

        private static string DescribeMetadata(CommandResponseMetadata? metadata)
        {
            if (metadata == null) return "<null>";
            string userData = metadata.UserData == null
                ? "<null>"
                : "{" + string.Join(", ", metadata.UserData.Select(kv => $"\"{kv.Key}\"=\"{kv.Value}\"")) + "}";
            return $"ContentType='{metadata.ContentType}', UserData={userData}";
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
