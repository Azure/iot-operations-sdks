namespace Azure.Iot.Operations.TypeGenerator
{
    public class DecimalType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Decimal; }

        public DecimalType()
        {
        }
    }
}
