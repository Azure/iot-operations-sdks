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
        /// Specializes a <c>CommandExecutor</c> class for Command 'createDiscoveredAsset'.
        /// </summary>
        public class CreateDiscoveredAssetCommandExecutor : CommandExecutor<CreateDiscoveredAssetCommandRequest, CreateDiscoveredAssetCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CreateDiscoveredAssetCommandExecutor"/> class.
            /// </summary>
            internal CreateDiscoveredAssetCommandExecutor(IMqttPubSubClient mqttClient)
                : base(mqttClient, "createDiscoveredAsset", new Utf8JsonSerializer())
            {
            }
        }
    }
}
