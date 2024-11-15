namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class DecimalType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Decimal; }

        public DecimalType()
        {
        }
    }
}
