using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfileCredentials
    {
        internal AssetEndpointProfileCredentials(string? username, byte[]? password, X509Certificate2? certificate)
        {
            Username = username;
            Password = password;
            Certificate = certificate;
        }

        public X509Certificate2? Certificate { get; private set; }

        public string? Username { get; private set; }

        public byte[]? Password { get; private set; }
    }
}
