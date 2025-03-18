namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class FloatType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Float; }

        public FloatType()
        {
        }
    }
}
