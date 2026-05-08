// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ISchemaStandardizer
    {
        SerializationFormat SerializationFormat { get; }

        bool TryGetStandardizedSchemas(Dictionary<string, string> schemaTextsByName, ErrorLog errorLog, out List<SchemaType> schemaTypes);
    }
}
