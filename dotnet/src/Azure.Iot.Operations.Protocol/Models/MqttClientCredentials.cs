
namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCredentials(string userName, byte[]? password = null) : IMqttClientCredentialsProvider
    {
        private readonly string _userName = userName;
        private readonly byte[]? _password = password;

        public string GetUserName(MqttClientOptions clientOptions)
        {
            return _userName;
        }

        public byte[]? GetPassword(MqttClientOptions clientOptions)
        {
            return _password;
        }
    }
}
