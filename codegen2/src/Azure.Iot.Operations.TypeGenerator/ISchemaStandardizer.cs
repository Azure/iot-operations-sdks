namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ISchemaStandardizer
    {
        SerializationFormat SerializationFormat { get; }

        List<SchemaType> GetStandardizedSchemas(Dictionary<string, string> schemaTextsByName);
    }
}
