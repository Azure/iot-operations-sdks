namespace Azure.Iot.Operations.TypeGenerator
{
    public class UuidType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Uuid; }

        public UuidType()
        {
        }
    }
}
