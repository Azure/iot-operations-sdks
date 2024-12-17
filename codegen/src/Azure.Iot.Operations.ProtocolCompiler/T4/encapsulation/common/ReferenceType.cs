namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ReferenceType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Reference; }

        public ReferenceType(string schemaName, bool isNullable = true)
        {
            SchemaName = schemaName;
            IsNullable = isNullable;
        }

        public string SchemaName { get; }

        public bool IsNullable { get; }
    }
}
