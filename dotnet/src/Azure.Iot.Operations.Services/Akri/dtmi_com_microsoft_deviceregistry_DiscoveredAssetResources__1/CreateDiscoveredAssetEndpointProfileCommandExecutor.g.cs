/* Code generated by Azure.Iot.Operations.ProtocolCompiler; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using Azure.Iot.Operations.Services.Akri;

    public static partial class DiscoveredAssetResources
    {
        /// <summary>
        /// Specializes a <c>CommandExecutor</c> class for Command 'createDiscoveredAssetEndpointProfile'.
        /// </summary>
        public class CreateDiscoveredAssetEndpointProfileCommandExecutor : CommandExecutor<CreateDiscoveredAssetEndpointProfileCommandRequest, CreateDiscoveredAssetEndpointProfileCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CreateDiscoveredAssetEndpointProfileCommandExecutor"/> class.
            /// </summary>
            internal CreateDiscoveredAssetEndpointProfileCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "createDiscoveredAssetEndpointProfile", new Utf8JsonSerializer())
            {
            }
        }
    }
}
