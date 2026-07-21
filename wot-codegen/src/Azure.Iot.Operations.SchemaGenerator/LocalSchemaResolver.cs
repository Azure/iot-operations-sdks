// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal class LocalSchemaResolver
    {
        private readonly ErrorReporter errorReporter;
        private readonly Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions;

        internal LocalSchemaResolver(ErrorReporter errorReporter, Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions)
        {
            this.errorReporter = errorReporter;
            this.schemaDefinitions = schemaDefinitions;
        }

        internal bool TryResolve(ValueTracker<TDDataSchema> dataSchema, IReadOnlyCollection<string>? referenceChain, out ValueTracker<TDDataSchema>? resolvedSchema, out string? directSchemaKey, out List<string> resolvedReferenceChain)
        {
            if (dataSchema.Value.LocalRef?.Value == null)
            {
                resolvedSchema = dataSchema;
                directSchemaKey = null;
                resolvedReferenceChain = referenceChain?.ToList() ?? new List<string>();
                return true;
            }

            return TryResolveLocalReference(dataSchema.Value.LocalRef, referenceChain, out resolvedSchema, out directSchemaKey, out resolvedReferenceChain);
        }

        internal bool TryResolveLocalReference(ValueTracker<StringHolder> localRef, IReadOnlyCollection<string>? referenceChain, out ValueTracker<TDDataSchema>? resolvedSchema, out string? directSchemaKey, out List<string> resolvedReferenceChain)
        {
            resolvedSchema = null;
            directSchemaKey = null;
            resolvedReferenceChain = referenceChain?.ToList() ?? new List<string>();

            string localRefValue = localRef.Value.Value;
            if (string.IsNullOrWhiteSpace(localRefValue))
            {
                this.errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Data schema '{TDDataSchema.LocalRefName}' property has empty value.", localRef.TokenIndex);
                return false;
            }

            if (!TDDataSchema.TryGetLocalRefSchemaKey(localRefValue, out string? schemaKey, out string? error))
            {
                this.errorReporter.ReportError(ErrorCondition.PropertyInvalid, error!, localRef.TokenIndex);
                return false;
            }

            string currentSchemaKey = schemaKey;

            while (true)
            {
                if (resolvedReferenceChain.Contains(currentSchemaKey))
                {
                    this.errorReporter.ReportError(ErrorCondition.Interminable, $"Interminable loop in '{TDDataSchema.LocalRefName}' references: {string.Join(" -> ", resolvedReferenceChain.Append(currentSchemaKey))}.", localRef.TokenIndex);
                    return false;
                }

                resolvedReferenceChain.Add(currentSchemaKey);

                if (this.schemaDefinitions == null)
                {
                    this.errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.LocalRefName}' property must refer to key in '{TDThing.SchemaDefinitionsName}' property, but Thing Model has no '{TDThing.SchemaDefinitionsName}' property.", localRef.TokenIndex);
                    return false;
                }

                if (!this.schemaDefinitions.TryGetValue(currentSchemaKey, out resolvedSchema))
                {
                    this.errorReporter.ReportError(ErrorCondition.ItemNotFound, $"Data schema '{TDDataSchema.LocalRefName}' property refers to non-existent key '{currentSchemaKey}' in '{TDThing.SchemaDefinitionsName}' property.", localRef.TokenIndex);
                    return false;
                }

                if (resolvedSchema.Value.LocalRef?.Value == null)
                {
                    directSchemaKey = currentSchemaKey;
                    return true;
                }

                if (string.IsNullOrWhiteSpace(resolvedSchema.Value.LocalRef.Value.Value))
                {
                    this.errorReporter.ReportError(ErrorCondition.PropertyEmpty, $"Data schema '{TDDataSchema.LocalRefName}' property has empty value.", resolvedSchema.Value.LocalRef.TokenIndex);
                    return false;
                }

                if (!TDDataSchema.TryGetLocalRefSchemaKey(resolvedSchema.Value.LocalRef.Value.Value, out string? nextSchemaKey, out error))
                {
                    this.errorReporter.ReportError(ErrorCondition.PropertyInvalid, error!, resolvedSchema.Value.LocalRef.TokenIndex);
                    return false;
                }

                currentSchemaKey = nextSchemaKey;
            }
        }
    }
}
