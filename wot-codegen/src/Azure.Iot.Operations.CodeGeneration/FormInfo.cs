// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public record FormInfo(
        SerializationFormat Format,
        bool HasErrorResponse,
        string? ErrorRespName,
        ValueTracker<TDDataSchema>? ErrorRespSchema,
        SerializationFormat ErrorRespFormat,
        string? HeaderInfoName,
        ValueTracker<TDDataSchema>? HeaderInfoSchema,
        SerializationFormat HeaderInfoFormat,
        string? HeaderCodeName,
        ValueTracker<TDDataSchema>? HeaderCodeSchema,
        string? ServiceGroupId,
        string? TopicPattern,
        bool IncludeInherited)
    {
        public static FormInfo? CreateFromForm(ErrorReporter errorReporter, TDForm? form, Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions)
        {
            if (form == null)
            {
                return null;
            }

            SerializationFormat format = ThingSupport.ContentTypeToFormat(errorReporter, form.ContentType);

            bool hasErrorResponse = form.AdditionalResponses?.Elements?.Any(r => !(r.Value.Success?.Value.Value ?? false)) ?? false;

            ValueTracker<TDSchemaReference>? errorSchemaRef = form.AdditionalResponses?.Elements?.FirstOrDefault(r => !(r.Value.Success?.Value.Value ?? false) && r.Value.Schema != null);
            var (errorRespName, errorRespSchema, errorRespFormat) = GetSchemaAndFormat(errorReporter, errorSchemaRef?.Value, form, schemaDefinitions);

            ValueTracker<TDSchemaReference>? headerSchemaRef = form.HeaderInfo?.Elements?.FirstOrDefault(r => r.Value.Schema != null);
            var (headerInfoName, headerInfoSchema, headerInfoFormat) = GetSchemaAndFormat(errorReporter, headerSchemaRef?.Value, form, schemaDefinitions);

            ValueTracker<TDDataSchema>? headerCodeSchema = GetSchema(errorReporter, form.HeaderCode?.Value?.Value, schemaDefinitions);

            return new FormInfo(
                format,
                hasErrorResponse,
                errorRespName,
                errorRespSchema,
                errorRespFormat,
                headerInfoName,
                headerInfoSchema,
                headerInfoFormat,
                form.HeaderCode?.Value?.Value,
                headerCodeSchema,
                form.ServiceGroupId?.Value?.Value,
                form.Topic?.Value?.Value,
                form.IncludeInherited?.Value?.Value ?? false);
        }

        private static (string?, ValueTracker<TDDataSchema>?, SerializationFormat) GetSchemaAndFormat(ErrorReporter errorReporter, TDSchemaReference? schemaRef, TDForm? form, Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions)
        {
            string? schemaName = schemaRef?.Schema?.Value?.Value;
            SerializationFormat schemaFormat = ThingSupport.ContentTypeToFormat(errorReporter, schemaRef?.ContentType ?? form?.ContentType);

            ValueTracker<TDDataSchema>? schema = GetSchema(errorReporter, schemaName, schemaDefinitions);

            return (schemaName, schema, schemaFormat);
        }

        private static ValueTracker<TDDataSchema>? GetSchema(ErrorReporter errorReporter, string? schemaName, Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions)
        {
            if (schemaDefinitions == null ||
                !schemaDefinitions.TryGetValue(schemaName ?? string.Empty, out ValueTracker<TDDataSchema>? schema))
            {
                return null;
            }

            HashSet<string> referenceChain = new();
            if (schemaName != null)
            {
                referenceChain.Add(schemaName);
            }

            while (schema.Value.LocalRef?.Value != null)
            {
                ValueTracker<StringHolder> localRef = schema.Value.LocalRef;
                if (!TDDataSchema.TryGetLocalRefSchemaKey(localRef.Value.Value, out string? referencedSchemaName, out string? error))
                {
                    errorReporter.ReportError(ErrorCondition.PropertyInvalid, error!, localRef.TokenIndex);
                    return null;
                }

                if (!referenceChain.Add(referencedSchemaName))
                {
                    errorReporter.ReportError(ErrorCondition.Interminable, $"Interminable loop in '{TDDataSchema.LocalRefName}' references: {string.Join(" -> ", referenceChain.Append(referencedSchemaName))}.", localRef.TokenIndex);
                    return null;
                }

                if (!schemaDefinitions.TryGetValue(referencedSchemaName, out schema))
                {
                    errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.LocalRefName}' property refers to non-existent key '{referencedSchemaName}' in '{TDThing.SchemaDefinitionsName}' property.", localRef.TokenIndex);
                    return null;
                }
            }

            return schema;
        }
    }
}
