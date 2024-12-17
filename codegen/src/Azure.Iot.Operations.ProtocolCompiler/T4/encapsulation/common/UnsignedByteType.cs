namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class UnsignedByteType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.UnsignedByte; }

        public UnsignedByteType()
        {
        }
    }
}
