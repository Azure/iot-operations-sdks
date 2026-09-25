// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A message schema to register with the Edge Registry as a Schema Version.
    /// </summary>
    public class ConnectorEdgeRegistryMessageSchema
    {
        /// <summary>
        /// The Schema Group to store the schema under.
        /// </summary>
        public GroupId GroupId { get; }

        /// <summary>
        /// The Schema to store the Version under. Use <see cref="ResourceId.Derive"/> to turn an
        /// arbitrary identifier into a valid Resource identifier.
        /// </summary>
        public string SchemaId { get; }

        /// <summary>
        /// Queryable key/value pairs to add to the parent Schema.
        /// </summary>
        public IReadOnlyList<Label> SchemaLabels { get; }

        /// <summary>
        /// The attributes of the Schema Version to create.
        /// </summary>
        public SchemaVersionAttributes Version { get; }

        public ConnectorEdgeRegistryMessageSchema(
            string schemaId,
            SchemaVersionAttributes version,
            IReadOnlyList<Label>? schemaLabels = null,
            GroupId groupId = default)
        {
            SchemaId = schemaId;
            Version = version;
            SchemaLabels = schemaLabels ?? [];
            GroupId = groupId;
        }

        /// <summary>
        /// Builds the identifier that uniquely describes the schema of a dataset. Pass it through
        /// <see cref="ResourceId.Derive"/> to turn it into a valid Resource identifier.
        /// </summary>
        public static string DefaultDatasetSchemaId(string deviceName, string inboundEndpointName, string assetName, string datasetName)
            => $"{deviceName}:{inboundEndpointName}:{assetName}:dataset:{datasetName}";

        /// <summary>
        /// Builds the identifier that uniquely describes the schema of an event. Pass it through
        /// <see cref="ResourceId.Derive"/> to turn it into a valid Resource identifier.
        /// </summary>
        public static string DefaultEventSchemaId(string deviceName, string inboundEndpointName, string assetName, string eventGroupName, string eventName)
            => $"{deviceName}:{inboundEndpointName}:{assetName}:event:{eventGroupName}:{eventName}";
    }
}
