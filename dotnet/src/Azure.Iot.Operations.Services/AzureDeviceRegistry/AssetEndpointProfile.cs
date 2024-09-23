using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfile
    {
        //TODO which fields are actually optional? Just additional configuration, right?
        public AssetEndpointProfile(string targetAddress, string authenticationMethod, string endpointProfileType)
        {
            TargetAddress = targetAddress;
            AuthenticationMethod = authenticationMethod;
            EndpointProfileType = endpointProfileType;
        }

        public string TargetAddress { get; set; }
        
        public string AuthenticationMethod { get; set; }

        public string EndpointProfileType { get; set; }

        //TODO this json structure is completely arbitrary from connector to connector, right?
        public JsonDocument? AdditionalConfiguration { get; set; }

        public AssetEndpointProfileCredentials? Credentials { get; set; }
    }
}
