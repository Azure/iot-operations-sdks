namespace Akri.Dtdl.Codegen
{
    public class ArrayType : SchemaType
    {
        public ArrayType(SchemaType elementSchema)
        {
            ElementSchema = elementSchema;
        }

        public SchemaType ElementSchema { get; set; }
    }
}
