namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor
{
    public class ArrayTypeInfo : SchemaTypeInfo
    {
        public ArrayTypeInfo(string schemaName, SchemaTypeInfo elementSchema)
            : base(schemaName)
        {
            ElementSchema = elementSchema;
        }

        public SchemaTypeInfo ElementSchema { get; set; }
    }
}
