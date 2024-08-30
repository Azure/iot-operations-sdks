namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientWebSocketProxyOptions
    {
        public string Address { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? Domain { get; set; }

        public bool BypassOnLocal { get; set; }

        public bool UseDefaultCredentials { get; set; }

        public string[]? BypassList { get; set; }

        public MqttClientWebSocketProxyOptions(string address) 
        { 
            Address = address;
        }
    }
}