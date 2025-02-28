namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class DateTimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.DateTime; }

        public DateTimeType()
        {
        }
    }
}
