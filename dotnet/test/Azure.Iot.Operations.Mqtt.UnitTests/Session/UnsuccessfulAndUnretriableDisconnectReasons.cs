using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulAndUnretriableDisconnectReasons : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttClientDisconnectReason.MalformedPacket };
            yield return new object[] { MqttClientDisconnectReason.ProtocolError };
            yield return new object[] { MqttClientDisconnectReason.NotAuthorized };
            yield return new object[] { MqttClientDisconnectReason.BadAuthenticationMethod };
            yield return new object[] { MqttClientDisconnectReason.SessionTakenOver };
            yield return new object[] { MqttClientDisconnectReason.TopicFilterInvalid };
            yield return new object[] { MqttClientDisconnectReason.TopicNameInvalid };
            yield return new object[] { MqttClientDisconnectReason.TopicAliasInvalid };
            yield return new object[] { MqttClientDisconnectReason.PacketTooLarge };
            yield return new object[] { MqttClientDisconnectReason.PayloadFormatInvalid };
            yield return new object[] { MqttClientDisconnectReason.RetainNotSupported };
            yield return new object[] { MqttClientDisconnectReason.QosNotSupported };
            yield return new object[] { MqttClientDisconnectReason.ServerMoved };
            yield return new object[] { MqttClientDisconnectReason.SharedSubscriptionsNotSupported };
            yield return new object[] { MqttClientDisconnectReason.SubscriptionIdentifiersNotSupported };
            yield return new object[] { MqttClientDisconnectReason.WildcardSubscriptionsNotSupported };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
