namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class DateType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Date; }

        public DateType()
        {
        }
    }
}
