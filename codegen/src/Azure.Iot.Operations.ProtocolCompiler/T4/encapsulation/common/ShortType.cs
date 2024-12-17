namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class ShortType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Short; }

        public ShortType()
        {
        }
    }
}
