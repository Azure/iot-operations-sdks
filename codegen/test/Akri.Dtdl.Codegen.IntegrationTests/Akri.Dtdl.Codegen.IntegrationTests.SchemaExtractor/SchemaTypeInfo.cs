namespace Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor
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
