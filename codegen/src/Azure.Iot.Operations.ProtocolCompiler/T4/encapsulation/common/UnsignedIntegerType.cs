namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class UnsignedIntegerType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.UnsignedInteger; }

        public UnsignedIntegerType()
        {
        }
    }
}
