namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor
{
    public class MapTypeInfo : SchemaTypeInfo
    {
        public MapTypeInfo(string schemaName, SchemaTypeInfo valueSchema)
            : base(schemaName)
        {
            ValueSchema = valueSchema;
        }

        public SchemaTypeInfo ValueSchema { get; set; }
    }
}
