namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Serialization;

    public interface ISchemaStandardizer
    {
        SerializationFormat SerializationFormat { get; }

        IEnumerable<SchemaType> GetStandardizedSchemas(string schemaText, CodeName genNamespace, Func<string, string> retriever);
    }
}
