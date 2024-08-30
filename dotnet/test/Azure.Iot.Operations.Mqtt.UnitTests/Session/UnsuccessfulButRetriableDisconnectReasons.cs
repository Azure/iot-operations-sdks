using MQTTnet.Client;
using System.Collections;

namespace Azure.Iot.Operations.Protocol.Session.UnitTests
{
    public class UnsuccessfulButRetriableDisconnectReasons : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            // This is the default case that is given by MQTTnet when no DISCONNECT packet is actually received
            yield return new object[] { MqttClientDisconnectReason.NormalDisconnection };
            yield return new object[] { MqttClientDisconnectReason.AdministrativeAction };
            yield return new object[] { MqttClientDisconnectReason.DisconnectWithWillMessage };
            yield return new object[] { MqttClientDisconnectReason.UnspecifiedError };
            yield return new object[] { MqttClientDisconnectReason.ImplementationSpecificError };
            yield return new object[] { MqttClientDisconnectReason.ServerBusy };
            yield return new object[] { MqttClientDisconnectReason.ServerShuttingDown };
            yield return new object[] { MqttClientDisconnectReason.KeepAliveTimeout };
            yield return new object[] { MqttClientDisconnectReason.ReceiveMaximumExceeded };
            yield return new object[] { MqttClientDisconnectReason.MessageRateTooHigh };
            yield return new object[] { MqttClientDisconnectReason.QuotaExceeded };
            yield return new object[] { MqttClientDisconnectReason.UseAnotherServer };
            yield return new object[] { MqttClientDisconnectReason.ConnectionRateExceeded };
            yield return new object[] { MqttClientDisconnectReason.MaximumConnectTime };

        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
