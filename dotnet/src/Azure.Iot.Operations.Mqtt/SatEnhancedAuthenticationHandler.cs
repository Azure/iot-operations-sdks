// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using MQTTnet;

namespace Azure.Iot.Operations.Mqtt
{
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
