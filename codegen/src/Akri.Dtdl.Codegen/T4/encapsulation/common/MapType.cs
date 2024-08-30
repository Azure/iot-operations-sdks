namespace Akri.Dtdl.Codegen
{
    public class MapType : SchemaType
    {
        public MapType(SchemaType valueSchema)
        {
            ValueSchema = valueSchema;
        }

        public SchemaType ValueSchema { get; set; }
    }
}
