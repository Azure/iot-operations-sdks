namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class StringType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.String; }

        public StringType()
        {
        }
    }
}
