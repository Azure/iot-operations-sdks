// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.Format;
using SchemaType = Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaType;


namespace Azure.Iot.Operations.Connector
{
    public class DatasetMessageSchema
    {
        public string SchemaContent { get; }
        
        public SchemaFormat SchemaFormat { get; }
        
        public SchemaType SchemaType { get; }
        
        public string? Version { get; }
        
        public Dictionary<string, string>? Tags { get; }

        public DatasetMessageSchema(
            string schemaContent,
            SchemaFormat schemaFormat,
            SchemaType schemaType,
            string? version,
            Dictionary<string, string>? tags)
        {
            SchemaContent = schemaContent;
            schemaFormat = SchemaFormat;
            SchemaType = schemaType;
            Version = version;
            Tags = tags;
        }
    }
}
