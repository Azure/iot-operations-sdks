using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AssetEndpointProfile
    {
        //TODO which fields are actually optional? Just additional configuration, right?
        public AssetEndpointProfile(string targetAddress, string authenticationMethod, string endpointProfileType, JsonDocument? additionalConfigurations)
        {
            TargetAddress = targetAddress;
            AuthenticationMethod = authenticationMethod;
            EndpointProfileType = endpointProfileType;
            AdditionalConfigurations = additionalConfigurations;
        }

        string TargetAddress { get; set; }
        
        string AuthenticationMethod { get; set; }

        //TODO is this related to AEP? It doesn't include AEP in the path anywhere?
        string EndpointProfileType { get; set; }

        //TODO this json structure is completely arbitrary from connector to connector, right?
        JsonDocument? AdditionalConfigurations { get; set; }
    }
}
