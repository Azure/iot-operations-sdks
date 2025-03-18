namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class BooleanType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Boolean; }

        public BooleanType()
        {
        }
    }
}
