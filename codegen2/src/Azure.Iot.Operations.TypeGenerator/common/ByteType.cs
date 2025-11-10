namespace Azure.Iot.Operations.TypeGenerator
{
    internal class ByteType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Byte; }

        internal ByteType(bool orNull)
            : base(orNull)
        {
        }
    }
}
