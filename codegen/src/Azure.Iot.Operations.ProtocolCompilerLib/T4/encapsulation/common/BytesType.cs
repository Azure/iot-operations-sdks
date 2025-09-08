namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class BytesType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Bytes; }

        public BytesType()
        {
        }
    }
}
