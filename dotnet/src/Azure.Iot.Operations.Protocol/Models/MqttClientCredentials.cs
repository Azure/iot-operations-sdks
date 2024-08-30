
namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCredentials : IMqttClientCredentialsProvider
    {
        readonly string _userName;
        readonly byte[]? _password;

        public MqttClientCredentials(string userName, byte[]? password = null)
        {
            _userName = userName;
            _password = password;
        }

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
