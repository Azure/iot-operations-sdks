namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System.Collections.Generic;

    public interface ISchemaStandardizer
    {
        SerializationFormat SerializationFormat { get; }

        IEnumerable<SchemaType> GetStandardizedSchemas(string schemaFilePath, CodeName genNamespace);
    }
}
