// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet.Protocol;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulButRetriableConnackCodesClassData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { MqttConnectReasonCode.ServerUnavailable };
            yield return new object[] { MqttConnectReasonCode.ServerBusy };
            yield return new object[] { MqttConnectReasonCode.QuotaExceeded };
            yield return new object[] { MqttConnectReasonCode.ConnectionRateExceeded };
            yield return new object[] { MqttConnectReasonCode.UnspecifiedError };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
