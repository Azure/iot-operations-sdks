// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public partial class ObjectJsonSchema : ISchemaTemplateTransform
    {
        private readonly JsonSchemaSupport schemaSupport;
        private readonly string schemaName;
        private readonly ObjectSpec objectSpec;

        internal ObjectJsonSchema(JsonSchemaSupport schemaSupport, string schemaName, ObjectSpec objectSpec)
        {
            this.schemaSupport = schemaSupport;
            this.schemaName = schemaName;
            this.objectSpec = objectSpec;
        }

        public SerializationFormat Format { get => SerializationFormat.Json; }

        public string FileName { get => $"{this.schemaName}.{JsonSchemaSupport.JsonSchemaSuffix}"; }
    }
}
