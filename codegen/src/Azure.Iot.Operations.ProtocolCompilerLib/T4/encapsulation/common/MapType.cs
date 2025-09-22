namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class MapType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Map; }

        public MapType(SchemaType valueSchema, bool nullValues)
        {
            ValueSchema = valueSchema;
            NullValues = nullValues;
        }

        public SchemaType ValueSchema { get; set; }

        public bool NullValues { get; set; }
    }
}
