// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Mqtt.Session.Exceptions
{
    /// <summary>
    /// This exception is thrown when an MQTT session could not be recovered by the client before it expired on the broker.
    /// </summary>
    /// <remarks>
    /// The session expiry interval can be set when first establishing a connection. If the client loses connection to the broker and then that interval passes 
    /// without the client successfully reconnecting, then the broker will discard the session. Upon a successful reconnection after this happens, this exception
    /// will be given by <see cref="IMqttSessionClient.DisconnectedAsync"/>.
    /// 
    /// To avoid this exception, longer values of the session expiry interval are recommended.
    /// </remarks>
    public class MqttSessionExpiredException : Exception
    {
        public MqttSessionExpiredException()
        {
        }

        public MqttSessionExpiredException(string? message) : base(message)
        {
        }

        public MqttSessionExpiredException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
