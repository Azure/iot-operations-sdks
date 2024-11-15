namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ByteType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Byte; }

        public ByteType()
        {
        }
    }
}
