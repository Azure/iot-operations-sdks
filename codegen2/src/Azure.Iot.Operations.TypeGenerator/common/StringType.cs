namespace Azure.Iot.Operations.TypeGenerator
{
    public class StringType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.String; }

        public StringType()
        {
        }
    }
}
