namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public ReferenceType(string schemaName)
        {
            SchemaName = schemaName;
        }

        public string SchemaName { get; }
    }
}
