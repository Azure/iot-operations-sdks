// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet.Client;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulAndUnretriableUnsubackCodesClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttClientUnsubscribeResultCode.ImplementationSpecificError };
            yield return new object[] { MqttClientUnsubscribeResultCode.NotAuthorized };
            yield return new object[] { MqttClientUnsubscribeResultCode.PacketIdentifierInUse};
            yield return new object[] { MqttClientUnsubscribeResultCode.TopicFilterInvalid };
            yield return new object[] { MqttClientUnsubscribeResultCode.UnspecifiedError };
            yield return new object[] { MqttClientUnsubscribeResultCode.NoSubscriptionExisted };

        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
