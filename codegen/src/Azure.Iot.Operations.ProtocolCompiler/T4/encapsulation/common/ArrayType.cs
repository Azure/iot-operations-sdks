namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ArrayType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Array; }

        public ArrayType(SchemaType elementSchema)
        {
            ElementSchema = elementSchema;
        }

        public SchemaType ElementSchema { get; set; }
    }
}
