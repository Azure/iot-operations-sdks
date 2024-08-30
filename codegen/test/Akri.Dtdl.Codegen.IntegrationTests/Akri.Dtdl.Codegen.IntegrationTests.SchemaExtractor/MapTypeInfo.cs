namespace Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor
{
    public class MapTypeInfo : SchemaTypeInfo
    {
        public MapTypeInfo(string schemaName, SchemaTypeInfo valueSchmema)
            : base(schemaName)
        {
            ValueSchmema = valueSchmema;
        }

        public SchemaTypeInfo ValueSchmema { get; set; }
    }
}
