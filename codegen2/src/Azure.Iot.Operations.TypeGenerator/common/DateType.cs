namespace Azure.Iot.Operations.TypeGenerator
{
    public class DateType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Date; }

        public DateType()
        {
        }
    }
}
