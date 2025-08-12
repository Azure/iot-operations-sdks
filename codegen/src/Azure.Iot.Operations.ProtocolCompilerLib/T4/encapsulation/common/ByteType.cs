namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class ByteType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Byte; }

        public ByteType()
        {
        }
    }
}
