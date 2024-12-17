using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;


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
