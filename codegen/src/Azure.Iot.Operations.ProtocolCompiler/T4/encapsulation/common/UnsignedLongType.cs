namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class UnsignedLongType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.UnsignedLong; }

        public UnsignedLongType()
        {
        }
    }
}
