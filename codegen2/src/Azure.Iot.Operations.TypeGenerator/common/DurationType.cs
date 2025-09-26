namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DurationType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Duration; }

        internal DurationType()
        {
        }
    }
}
