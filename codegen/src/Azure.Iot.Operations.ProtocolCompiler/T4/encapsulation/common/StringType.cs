namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class StringType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.String; }

        public StringType()
        {
        }
    }
}
