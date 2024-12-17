using MQTTnet.Client;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulAndUnretriableSubackCodesClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttClientSubscribeResultCode.ImplementationSpecificError };
            yield return new object[] { MqttClientSubscribeResultCode.NotAuthorized };
            yield return new object[] { MqttClientSubscribeResultCode.PacketIdentifierInUse};
            yield return new object[] { MqttClientSubscribeResultCode.QuotaExceeded };
            yield return new object[] { MqttClientSubscribeResultCode.SharedSubscriptionsNotSupported };
            yield return new object[] { MqttClientSubscribeResultCode.SubscriptionIdentifiersNotSupported };
            yield return new object[] { MqttClientSubscribeResultCode.TopicFilterInvalid };
            yield return new object[] { MqttClientSubscribeResultCode.WildcardSubscriptionsNotSupported };
            yield return new object[] { MqttClientSubscribeResultCode.UnspecifiedError };

        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
