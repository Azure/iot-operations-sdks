namespace Akri.Dtdl.Codegen
{
    public class ReferenceType : SchemaType
    {
        public ReferenceType(string schemaName)
        {
            SchemaName = schemaName;
        }

        public string SchemaName { get; }
    }
}
