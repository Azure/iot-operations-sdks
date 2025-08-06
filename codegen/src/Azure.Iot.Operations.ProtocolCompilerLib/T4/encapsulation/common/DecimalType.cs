namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class DecimalType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Decimal; }

        public DecimalType()
        {
        }
    }
}
