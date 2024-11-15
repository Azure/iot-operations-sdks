namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class BytesType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Bytes; }

        public BytesType()
        {
        }
    }
}
