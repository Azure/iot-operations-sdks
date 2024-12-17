namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class LongType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Long; }

        public LongType()
        {
        }
    }
}
