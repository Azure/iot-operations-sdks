using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.Akri;

internal class DiscoveredAssetResourcesClientStub(IMqttPubSubClient mqttClient) : dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.DiscoveredAssetResources.Client(mqttClient)
{
}