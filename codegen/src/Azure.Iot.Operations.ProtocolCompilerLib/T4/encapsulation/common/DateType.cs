namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class DateType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Date; }

        public DateType()
        {
        }
    }
}
