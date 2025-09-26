namespace Azure.Iot.Operations.TypeGenerator
{
    internal class ArrayType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Array; }

        internal ArrayType(SchemaType elementSchema)
        {
            ElementSchema = elementSchema;
        }

        internal SchemaType ElementSchema { get; set; }
    }
}
