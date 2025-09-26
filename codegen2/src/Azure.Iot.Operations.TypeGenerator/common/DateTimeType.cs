namespace Azure.Iot.Operations.TypeGenerator
{
    public class DateTimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.DateTime; }

        public DateTimeType()
        {
        }
    }
}
