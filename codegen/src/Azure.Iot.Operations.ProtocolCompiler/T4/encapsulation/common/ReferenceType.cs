namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public ReferenceType(string schemaName, bool isNullable = true)
        {
            SchemaName = schemaName;
            IsNullable = isNullable;
        }

        public string SchemaName { get; }

        public bool IsNullable { get; }
    }
}
