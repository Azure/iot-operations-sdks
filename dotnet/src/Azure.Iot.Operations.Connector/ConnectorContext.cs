// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.StateStore;

namespace Azure.Iot.Operations.Connector
{
    internal class ConnectorContext
    {
        internal ApplicationContext ApplicationContext { get; set; }

        internal IStateStoreClient StateStoreClient { get; set; }

        internal IMqttClient MqttClient { get; set; }

        internal ISchemaRegistryClient SchemaRegistryClient { get; set; }

        internal IAzureDeviceRegistryClientWrapper AzureDeviceRegistryClient { get; set; }

        internal ConnectorArtifacts ConnectorArtifacts { get; set; }

        public ConnectorContext(
            ApplicationContext applicationContext,
            IStateStoreClient stateStoreClient,
            IMqttClient mqttClient,
            ISchemaRegistryClient schemaRegistryClient,
            IAzureDeviceRegistryClientWrapper azureDeviceRegistryClient,
            ConnectorArtifacts connectorArtifacts)
        {
            ApplicationContext = applicationContext;
            StateStoreClient = stateStoreClient;
            MqttClient = mqttClient;
            SchemaRegistryClient = schemaRegistryClient;
            AzureDeviceRegistryClient = azureDeviceRegistryClient;
            ConnectorArtifacts = connectorArtifacts;
        }
    }
}
