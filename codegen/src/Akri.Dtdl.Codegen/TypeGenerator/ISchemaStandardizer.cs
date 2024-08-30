namespace Akri.Dtdl.Codegen
{
    using System.Collections.Generic;
    using System.IO;

    public interface ISchemaStandardizer
    {
        IEnumerable<SchemaType> GetStandardizedSchemas(StreamReader schemaReader);
    }
}
