namespace Azure.Iot.Operations.TypeGenerator
{
    internal class BytesType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Bytes; }

        internal BytesType()
        {
        }
    }
}
