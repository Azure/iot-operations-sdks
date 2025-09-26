namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DateType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Date; }

        internal DateType()
        {
        }
    }
}
