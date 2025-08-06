namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class DateTimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.DateTime; }

        public DateTimeType()
        {
        }
    }
}
