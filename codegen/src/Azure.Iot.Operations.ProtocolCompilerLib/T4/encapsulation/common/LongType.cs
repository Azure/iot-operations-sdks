namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class LongType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Long; }

        public LongType()
        {
        }
    }
}
