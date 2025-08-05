namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class IntegerType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Integer; }

        public IntegerType()
        {
        }
    }
}
