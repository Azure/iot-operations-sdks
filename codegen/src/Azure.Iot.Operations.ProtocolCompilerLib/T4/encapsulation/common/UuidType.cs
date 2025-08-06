namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class UuidType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Uuid; }

        public UuidType()
        {
        }
    }
}
