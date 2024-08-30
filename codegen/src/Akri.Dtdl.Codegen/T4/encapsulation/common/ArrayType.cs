namespace Akri.Dtdl.Codegen
{
    public class ArrayType : SchemaType
    {
        public ArrayType(SchemaType elementSchmema)
        {
            ElementSchmema = elementSchmema;
        }

        public SchemaType ElementSchmema { get; set; }
    }
}
