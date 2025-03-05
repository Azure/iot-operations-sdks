/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources
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
        public class CreateDiscoveredAssetCommandExecutor : CommandExecutor<CreateDiscoveredAssetRequestPayload, CreateDiscoveredAssetResponsePayload>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CreateDiscoveredAssetCommandExecutor"/> class.
            /// </summary>
            public CreateDiscoveredAssetCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
                : base(applicationContext, mqttClient, "createDiscoveredAsset", new Utf8JsonSerializer())
            {
                TopicTokenMap["modelId"] = "dtmi:com:microsoft:deviceregistry:DiscoveredAssetResources;1";
                if (mqttClient.ClientId != null)
                {
                    TopicTokenMap["executorId"] = mqttClient.ClientId;
                }
                TopicTokenMap["commandName"] = "createDiscoveredAsset";
            }
        }
    }
}
