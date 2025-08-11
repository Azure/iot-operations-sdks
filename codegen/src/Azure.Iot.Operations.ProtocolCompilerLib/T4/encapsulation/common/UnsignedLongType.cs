namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class UnsignedLongType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.UnsignedLong; }

        public UnsignedLongType()
        {
        }
    }
}
