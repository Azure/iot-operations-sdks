namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UnsignedLongType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.UnsignedLong; }

        internal UnsignedLongType(bool orNull)
            : base(orNull)
        {
        }
    }
}
