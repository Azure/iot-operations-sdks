// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet.Client;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulAndUnretriablePubackCodesClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttClientPublishReasonCode.UnspecifiedError};
            yield return new object[] { MqttClientPublishReasonCode.TopicNameInvalid };
            yield return new object[] { MqttClientPublishReasonCode.PayloadFormatInvalid };
            yield return new object[] { MqttClientPublishReasonCode.PacketIdentifierInUse };
            yield return new object[] { MqttClientPublishReasonCode.ImplementationSpecificError };
            yield return new object[] { MqttClientPublishReasonCode.NoMatchingSubscribers };
            yield return new object[] { MqttClientPublishReasonCode.PacketIdentifierInUse };
            yield return new object[] { MqttClientPublishReasonCode.QuotaExceeded };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
