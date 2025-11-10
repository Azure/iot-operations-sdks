namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DoubleType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Double; }

        internal DoubleType(bool orNull)
            : base(orNull)
        {
        }
    }
}
