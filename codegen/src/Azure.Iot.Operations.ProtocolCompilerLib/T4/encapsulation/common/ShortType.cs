namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class ShortType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Short; }

        public ShortType()
        {
        }
    }
}
