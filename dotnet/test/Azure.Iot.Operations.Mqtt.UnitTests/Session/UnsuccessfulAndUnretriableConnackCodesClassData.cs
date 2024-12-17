// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet.Protocol;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulAndUnretriableConnackCodesClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttConnectReasonCode.MalformedPacket };
            yield return new object[] { MqttConnectReasonCode.ProtocolError };
            yield return new object[] { MqttConnectReasonCode.UnsupportedProtocolVersion };
            yield return new object[] { MqttConnectReasonCode.ClientIdentifierNotValid };
            yield return new object[] { MqttConnectReasonCode.BadUserNameOrPassword };
            yield return new object[] { MqttConnectReasonCode.Banned };
            yield return new object[] { MqttConnectReasonCode.BadAuthenticationMethod };
            yield return new object[] { MqttConnectReasonCode.TopicNameInvalid };
            yield return new object[] { MqttConnectReasonCode.PacketTooLarge };
            yield return new object[] { MqttConnectReasonCode.PayloadFormatInvalid };
            yield return new object[] { MqttConnectReasonCode.RetainNotSupported };
            yield return new object[] { MqttConnectReasonCode.QoSNotSupported };
            yield return new object[] { MqttConnectReasonCode.ServerMoved };
            yield return new object[] { MqttConnectReasonCode.ImplementationSpecificError };
            yield return new object[] { MqttConnectReasonCode.UseAnotherServer };
            yield return new object[] { MqttConnectReasonCode.NotAuthorized };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
