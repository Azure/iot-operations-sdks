using System;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Events
{
    public sealed class MqttClientConnectedEventArgs : EventArgs
    {
        public MqttClientConnectedEventArgs(MqttClientConnectResult connectResult)
        {
            ConnectResult = connectResult ?? throw new ArgumentNullException(nameof(connectResult));
        }

        /// <summary>
        ///     Gets the authentication result.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientConnectResult ConnectResult { get; }
    }
}
