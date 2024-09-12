namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor
{
    public class ArrayTypeInfo : SchemaTypeInfo
    {
        public ArrayTypeInfo(string schemaName, SchemaTypeInfo elementSchmema)
            : base(schemaName)
        {
            ElementSchmema = elementSchmema;
        }

        public SchemaTypeInfo ElementSchmema { get; set; }
    }
}
