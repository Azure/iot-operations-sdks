namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;
    using System.IO;

    public interface ISchemaStandardizer
    {
        IEnumerable<SchemaType> GetStandardizedSchemas(StreamReader schemaReader);
    }
}
