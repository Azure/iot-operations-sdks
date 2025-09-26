namespace Azure.Iot.Operations.TypeGenerator
{
    internal class MapType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Map; }

        internal MapType(SchemaType valueSchema, bool nullValues)
        {
            ValueSchema = valueSchema;
            NullValues = nullValues;
        }

        internal SchemaType ValueSchema { get; set; }

        internal bool NullValues { get; set; }
    }
}
