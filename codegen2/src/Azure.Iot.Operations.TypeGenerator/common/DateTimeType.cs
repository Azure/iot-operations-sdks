namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DateTimeType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.DateTime; }

        internal DateTimeType()
        {
        }
    }
}
