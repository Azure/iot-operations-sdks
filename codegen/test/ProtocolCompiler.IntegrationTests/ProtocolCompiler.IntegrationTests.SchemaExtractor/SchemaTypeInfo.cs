namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor
{
    public abstract class SchemaTypeInfo
    {
        public SchemaTypeInfo(string schemaName)
        {
            SchemaName = schemaName;
        }

        public string SchemaName { get; set; }
    }
}
