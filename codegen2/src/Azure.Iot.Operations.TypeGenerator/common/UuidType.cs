namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UuidType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Uuid; }

        internal UuidType()
        {
        }
    }
}
