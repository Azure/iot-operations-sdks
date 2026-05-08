// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Mqtt.Session.Exceptions
{
    /// <summary>
    /// This exception is thrown by a <see cref="MqttSessionClient"/> if and only if the <see cref="MqttSessionClientOptions"/> flag for <see cref="MqttSessionClientOptions.ThrowIfUsedWhenSessionInactive"/>
    /// is set and that session client attempts to publish/subscribe/unsubscribe while the session client's MQTT connection is closed and the session client is not attempting to reconnect.
    /// </summary>
    public class SessionClosedException : Exception
    {
        public SessionClosedException(string message) : base(message) { }
    }
}
