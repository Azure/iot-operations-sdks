namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class DurationType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Duration; }

        public DurationType()
        {
        }
    }
}
