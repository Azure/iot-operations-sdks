namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttClientChannelOptions
    {
        MqttClientTlsOptions TlsOptions { get; }
    }
}
