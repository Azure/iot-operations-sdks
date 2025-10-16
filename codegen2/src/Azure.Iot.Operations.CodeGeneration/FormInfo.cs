namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser.Model;

    public record FormInfo(
        SerializationFormat Format,
        bool HasErrorResponse,
        string? ErrorRespName,
        TDDataSchema? ErrorRespSchema,
        SerializationFormat ErrorRespFormat,
        string? HeaderInfoName,
        TDDataSchema? HeaderInfoSchema,
        SerializationFormat HeaderInfoFormat,
        string? HeaderCodeName,
        TDDataSchema? HeaderCodeSchema,
        string? ServiceGroupId,
        string? TopicPattern)
    {
        public static FormInfo? CreateFromForm(TDForm? form, Dictionary<string, TDDataSchema>? schemaDefinitions)
        {
            if (form == null)
            {
                return null;
            }

            SerializationFormat format = ThingSupport.ContentTypeToFormat(form.ContentType);

            bool hasErrorResponse = form.AdditionalResponses?.Any(r => !r.Success) ?? false;

            TDSchemaReference? errorSchemaRef = form.AdditionalResponses?.FirstOrDefault(r => !r.Success && r.Schema != null);
            var (errorRespName, errorRespSchema, errorRespFormat) = GetSchemaAndFormat(errorSchemaRef, form, schemaDefinitions);

            TDSchemaReference? headerSchemaRef = form.HeaderInfo?.FirstOrDefault(r => r.Schema != null);
            var (headerInfoName, headerInfoSchema, headerInfoFormat) = GetSchemaAndFormat(headerSchemaRef, form, schemaDefinitions);

            TDDataSchema? headerCodeSchema = GetSchema(form.HeaderCode, schemaDefinitions);

            return new FormInfo(
                format,
                hasErrorResponse,
                errorRespName,
                errorRespSchema,
                errorRespFormat,
                headerInfoName,
                headerInfoSchema,
                headerInfoFormat,
                form.HeaderCode,
                headerCodeSchema,
                form.ServiceGroupId,
                form.Topic);
        }

        private static (string?, TDDataSchema?, SerializationFormat) GetSchemaAndFormat(TDSchemaReference? schemaRef, TDForm? form, Dictionary<string, TDDataSchema>? schemaDefinitions)
        {
            string? schemaName = schemaRef?.Schema;
            SerializationFormat schemaFormat = ThingSupport.ContentTypeToFormat(schemaRef?.ContentType ?? form?.ContentType);

            TDDataSchema? schema = null;
            schemaDefinitions?.TryGetValue(schemaName ?? string.Empty, out schema);

            return (schemaName, schema, schemaFormat);
        }

        private static TDDataSchema? GetSchema(string? schemaName, Dictionary<string, TDDataSchema>? schemaDefinitions)
        {
            return schemaDefinitions?.TryGetValue(schemaName ?? string.Empty, out TDDataSchema? schema) ?? false ? schema : null;
        }
    }
}
