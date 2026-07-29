// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal static class SchemaGenerationSupport
    {
        internal static void AddSchemaReference(string schemaName, SerializationFormat format, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            if (!referencedSchemas.TryGetValue(schemaName, out HashSet<SerializationFormat>? formats) || formats == null)
            {
                formats = new HashSet<SerializationFormat>();
                referencedSchemas[schemaName] = formats;
            }

            formats.Add(format);
        }
    }
}
