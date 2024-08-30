namespace Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor
{
    using System.Collections.Generic;

    public class ObjectTypeInfo : SchemaTypeInfo
    {
        public ObjectTypeInfo(string schemaName, Dictionary<string, SchemaTypeInfo> fieldSchemas)
            : base(schemaName)
        {
            FieldSchemas = fieldSchemas;
        }

        public Dictionary<string, SchemaTypeInfo> FieldSchemas { get; set; }
    }
}
