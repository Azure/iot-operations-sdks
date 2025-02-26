namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class IntegerType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Integer; }

        public IntegerType()
        {
        }
    }
}
