namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser.Model;

    public record FormInfo(
        SerializationFormat Format,
        bool HasErrorResponse,
        string? ErrorSchemaName,
        TDDataSchema? ErrorSchema,
        SerializationFormat ErrorSchemaFormat,
        string? HeaderSchemaName,
        TDDataSchema? HeaderSchema,
        SerializationFormat HeaderSchemaFormat,
        string? TopicPattern)
    {
        public static FormInfo? CreateFromForm(TDForm? form, Dictionary<string, TDDataSchema>? schemaDefinitions)
        {
            if (form == null)
            {
                return null;
            }

            SerializationFormat format = ContentTypeToFormat(form.ContentType);

            bool hasErrorResponse = form.AdditionalResponses?.Any(r => !r.Success) ?? false;

            TDSchemaReference? errorSchemaRef = form.AdditionalResponses?.FirstOrDefault(r => !r.Success && r.Schema != null);
            var (errorSchemaName, errorSchema, errorSchemaFormat) = GetSchemaAndFormat(errorSchemaRef, form, schemaDefinitions);

            TDSchemaReference? headerSchemaRef = form.HeaderInfo?.FirstOrDefault(r => r.Schema != null);
            var (headerSchemaName, headerSchema, headerSchemaFormat) = GetSchemaAndFormat(headerSchemaRef, form, schemaDefinitions);

            return new FormInfo(format, hasErrorResponse, errorSchemaName, errorSchema, errorSchemaFormat, headerSchemaName, headerSchema, headerSchemaFormat, form.Topic);
        }

        private static (string?, TDDataSchema?, SerializationFormat) GetSchemaAndFormat(TDSchemaReference? schemaRef, TDForm? form, Dictionary<string, TDDataSchema>? schemaDefinitions)
        {
            string? schemaName = schemaRef?.Schema;
            SerializationFormat schemaFormat = ContentTypeToFormat(schemaRef?.ContentType ?? form?.ContentType);

            TDDataSchema? schema = null;
            schemaDefinitions?.TryGetValue(schemaName ?? string.Empty, out schema);

            return (schemaName, schema, schemaFormat);
        }

        private static SerializationFormat ContentTypeToFormat(string? contentType)
        {
            return contentType switch
            {
                TDValues.ContentTypeJson => SerializationFormat.Json,
                _ => SerializationFormat.None,
            };
        }
    }
}
