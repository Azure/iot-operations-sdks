// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using MQTTnet;

namespace Azure.Iot.Operations.Mqtt
{
    /// <summary>
    /// This defines how an MQTT client should respond upon receiving an MQTT auth packet when using SAT authentication.
    /// </summary>
    /// <remarks>
    /// The typical flow for SAT auth is that the client proactively sends an MQTT auth packet to the broker with updated
    /// credentials, and then the broker sends back an MQTT auth packet that simply details if the client's auth packet was
    /// accepted or not. The client doesn't need to respond with another auth packet, so this handler simply logs the status code
    /// in the received MQTT auth packet.
    /// </remarks>
    internal class SatEnhancedAuthenticationHandler : IMqttEnhancedAuthenticationHandler
    {

        public Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            if (eventArgs.ReasonCode == MQTTnet.Protocol.MqttAuthenticateReasonCode.Success)
            {
                Trace.TraceInformation("Received re-authentication response from MQTT broker with status {0}", eventArgs.ReasonCode);
            }
            else
            {
                Trace.TraceError("Received re-authentication response from MQTT broker with status {0} and reason string {1}", eventArgs.ReasonCode, eventArgs.ReasonString);
            }

            return Task.CompletedTask;
        }
    }
}
