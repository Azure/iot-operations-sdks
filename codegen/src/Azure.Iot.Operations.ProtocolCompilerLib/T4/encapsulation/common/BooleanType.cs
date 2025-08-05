namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class BooleanType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Boolean; }

        public BooleanType()
        {
        }
    }
}
