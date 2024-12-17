namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class DoubleType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Double; }

        public DoubleType()
        {
        }
    }
}
