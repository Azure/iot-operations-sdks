using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfileCredentials
    {
        internal AssetEndpointProfileCredentials(string? username, string? password, X509Certificate2? certificate)
        {
            Username = username;
            Password = password;
            Certificate = certificate;
        }

        public X509Certificate2? Certificate { get; private set; }

        public string? Username { get; private set; }

        //TODO probably should be char[] so it can be cleared from memory
        public string? Password { get; private set; }
    }
}
