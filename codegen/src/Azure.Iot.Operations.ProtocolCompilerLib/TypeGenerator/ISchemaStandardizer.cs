namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;

    public interface ISchemaStandardizer
    {
        SerializationFormat SerializationFormat { get; }

        IEnumerable<SchemaType> GetStandardizedSchemas(string schemaText, CodeName genNamespace, Func<string, string> retriever);
    }
}
