namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class UnsignedIntegerType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.UnsignedInteger; }

        public UnsignedIntegerType()
        {
        }
    }
}
