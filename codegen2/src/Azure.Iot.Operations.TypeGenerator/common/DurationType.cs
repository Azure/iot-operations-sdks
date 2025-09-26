namespace Azure.Iot.Operations.TypeGenerator
{
    public class DurationType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Duration; }

        public DurationType()
        {
        }
    }
}
