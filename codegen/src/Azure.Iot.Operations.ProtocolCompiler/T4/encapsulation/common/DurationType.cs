namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class DurationType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Duration; }

        public DurationType()
        {
        }
    }
}
