using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Net;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientWebSocketOptions : IMqttClientChannelOptions
    {
        public CookieContainer? CookieContainer { get; set; }

        public ICredentials? Credentials { get; set; }

        public MqttClientWebSocketProxyOptions? ProxyOptions { get; set; }

        public IDictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        public ICollection<string> SubProtocols { get; set; } = new List<string> { "mqtt" };

        public MqttClientTlsOptions TlsOptions { get; set; } = new MqttClientTlsOptions();

        public required string Uri { get; set; }

        public override string ToString()
        {
            return Uri;
        }

        /// <summary>
        ///     Gets or sets the keep alive interval for the Web Socket connection.
        ///     This is not related to the keep alive interval for the MQTT protocol.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = WebSocket.DefaultKeepAliveInterval;

        /// <summary>
        ///     Gets or sets whether the default (system) credentials should be used when connecting via Web Socket connection.
        ///     This is not related to the credentials which are used for the MQTT protocol.
        /// </summary>
        public bool UseDefaultCredentials { get; set; }
    }
}
