namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DecimalType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Decimal; }

        internal DecimalType(bool orNull)
            : base(orNull)
        {
        }
    }
}
