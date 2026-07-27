// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal class JsonSchemaSupport
    {
        internal const string JsonSchemaSuffix = "schema.json";

        private const string Iso8601DurationExample = "P3Y6M4DT12H30M5S";
        private const string DecimalExample = "1234567890.0987654321";
        private const string AnArbitraryString = "Pretty12345Tricky67890";
        private const string DecimalPattern = @"^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$";

        private readonly SchemaNamer schemaNamer;
        private readonly DirectoryInfo workingDir;
        private readonly LocalSchemaResolver localSchemaResolver;

        internal JsonSchemaSupport(SchemaNamer schemaNamer, DirectoryInfo workingDir, LocalSchemaResolver localSchemaResolver)
        {
            this.schemaNamer = schemaNamer;
            this.workingDir = workingDir;
            this.localSchemaResolver = localSchemaResolver;
        }

        internal string GetFragmented(string typeAndAddenda, bool require)
        {
            string addProps = require ? typeAndAddenda : $"\"anyOf\": [ {{ \"type\": \"null\" }}, {{ {typeAndAddenda} }} ]";
            return $"\"type\": \"object\", \"additionalProperties\": {{ {addProps} }}";
        }

        internal string GetReferencePath(string reference, string refBase)
        {
            return reference.Contains('/') ? Path.GetRelativePath(this.workingDir.FullName, Path.Combine(refBase, reference)).Replace('\\', '/') : $"./{reference}";
        }

        internal string GetTypeAndAddenda(ValueTracker<TDDataSchema> tdSchema, string backupSchemaName, string refBase, IReadOnlyCollection<string>? referenceChain = null)
        {
            if (tdSchema.Value.Ref?.Value != null)
            {
                return $"\"$ref\": \"{GetReferencePath(tdSchema.Value.Ref.Value.Value, refBase)}\"";
            }

            if (!this.localSchemaResolver.TryResolve(tdSchema, referenceChain, out ValueTracker<TDDataSchema>? resolvedSchema, out string? directSchemaKey, out List<string> resolvedReferenceChain))
            {
                return @"""type"": ""null""";
            }

            string effectiveBackupSchemaName = directSchemaKey ?? backupSchemaName;

            ValueTracker<TDDataSchema> activeSchema = resolvedSchema!;

            if ((activeSchema.Value.Type?.Value.Value == TDValues.TypeObject && activeSchema.Value.AdditionalProperties?.Value == null) ||
                (activeSchema.Value.Type?.Value.Value == TDValues.TypeString && activeSchema.Value.Enum != null))
            {
                return $"\"$ref\": \"{this.schemaNamer.ApplyBackupSchemaName(activeSchema.Value.Title?.Value.Value, effectiveBackupSchemaName)}.{JsonSchemaSuffix}\"";
            }

            switch (activeSchema.Value.Type?.Value.Value ?? string.Empty)
            {
                case TDValues.TypeObject:
                    return $"\"type\": \"object\", \"additionalProperties\": {{ {GetTypeAndAddenda(activeSchema.Value.AdditionalProperties!, effectiveBackupSchemaName, refBase, resolvedReferenceChain)} }}";
                case TDValues.TypeArray:
                    string itemsProp = activeSchema.Value.Items?.Value != null ? $", \"items\": {{ {GetTypeAndAddenda(activeSchema.Value.Items, effectiveBackupSchemaName, refBase, resolvedReferenceChain)} }}" : string.Empty;
                    return $"\"type\": \"array\"{itemsProp}";
                case TDValues.TypeString:
                    string formatProp = TDValues.FormatValues.Contains(activeSchema.Value.Format?.Value.Value ?? string.Empty) ? $", \"format\": \"{activeSchema.Value.Format!.Value.Value}\"" :
                        activeSchema.Value.Pattern?.Value != null && Regex.IsMatch(Iso8601DurationExample, activeSchema.Value.Pattern.Value.Value) && !Regex.IsMatch(AnArbitraryString, activeSchema.Value.Pattern.Value.Value) ? @", ""format"": ""duration""" : string.Empty;
                    string patternProp = activeSchema.Value.Pattern?.Value != null && Regex.IsMatch(DecimalExample, activeSchema.Value.Pattern.Value.Value) && !Regex.IsMatch(AnArbitraryString, activeSchema.Value.Pattern.Value.Value) ? $", \"pattern\": \"{DecimalPattern}\"" : string.Empty;
                    string encodingProp = activeSchema.Value.ContentEncoding?.Value.Value == TDValues.ContentEncodingBase64 ? @", ""contentEncoding"": ""base64""" : string.Empty;
                    string enumProp = activeSchema.Value.Enum?.Elements != null ? $", \"enum\": [ {string.Join(", ", activeSchema.Value.Enum.Elements.Select(e => $"\"{e.Value.Value}\""))} ]" : string.Empty;
                    return $"\"type\": \"string\"{formatProp}{patternProp}{encodingProp}{enumProp}";
                case TDValues.TypeNumber:
                    string numberFormat = activeSchema.Value.Minimum?.Value.Value >= -3.40e+38 && activeSchema.Value.Maximum?.Value.Value <= 3.40e+38 ? "float" : "double";
                    return $"\"type\": \"number\", \"format\": \"{numberFormat}\"";
                case TDValues.TypeInteger:
                    string minProp = activeSchema.Value.Minimum?.Value != null ? $", \"minimum\": {(long)activeSchema.Value.Minimum.Value.Value}" : string.Empty;
                    string maxProp = activeSchema.Value.Maximum?.Value != null ? $", \"maximum\": {(long)activeSchema.Value.Maximum.Value.Value}" : string.Empty;
                    return $"\"type\": \"integer\"{minProp}{maxProp}";
                case TDValues.TypeBoolean:
                    return @"""type"": ""boolean""";
            }

            return @"""type"": ""null""";
        }
    }
}
