namespace Azure.Iot.Operations.TypeGenerator
{
    public class BytesType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Bytes; }

        public BytesType()
        {
        }
    }
}
