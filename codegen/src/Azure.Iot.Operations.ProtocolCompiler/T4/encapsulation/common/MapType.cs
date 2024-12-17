namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class MapType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Map; }

        public MapType(SchemaType valueSchema)
        {
            ValueSchema = valueSchema;
        }

        public SchemaType ValueSchema { get; set; }
    }
}
