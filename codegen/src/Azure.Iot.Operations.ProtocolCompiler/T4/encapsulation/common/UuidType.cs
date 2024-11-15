namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class UuidType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Uuid; }

        public UuidType()
        {
        }
    }
}
