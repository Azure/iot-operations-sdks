// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ISchemaTemplateTransform
    {
        SerializationFormat Format { get; }

        string FileName { get; }

        string TransformText();
    }
}
