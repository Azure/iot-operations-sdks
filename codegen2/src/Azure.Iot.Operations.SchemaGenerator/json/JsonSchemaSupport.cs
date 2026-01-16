namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.IO;
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

        internal JsonSchemaSupport(SchemaNamer schemaNamer, DirectoryInfo workingDir)
        {
            this.schemaNamer = schemaNamer;
            this.workingDir = workingDir;
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

        internal string GetTypeAndAddenda(ValueTracker<TDDataSchema> tdSchema, string backupSchemaName, string refBase)
        {
            if (tdSchema.Value.Ref?.Value != null)
            {
                return $"\"$ref\": \"{GetReferencePath(tdSchema.Value.Ref.Value.Value, refBase)}\"";
            }

            if ((tdSchema.Value.Type?.Value.Value == TDValues.TypeObject && tdSchema.Value.AdditionalProperties?.Value == null) ||
                (tdSchema.Value.Type?.Value.Value == TDValues.TypeString && tdSchema.Value.Enum != null))
            {
                return $"\"$ref\": \"{this.schemaNamer.ApplyBackupSchemaName(tdSchema.Value.Title?.Value.Value, backupSchemaName)}.{JsonSchemaSuffix}\"";
            }

            switch (tdSchema.Value.Type?.Value.Value ?? string.Empty)
            {
                case TDValues.TypeObject:
                    return $"\"type\": \"object\", \"additionalProperties\": {{ {GetTypeAndAddenda(tdSchema.Value.AdditionalProperties!, backupSchemaName, refBase)} }}";
                case TDValues.TypeArray:
                    string itemsProp = tdSchema.Value.Items?.Value != null ? $", \"items\": {{ {GetTypeAndAddenda(tdSchema.Value.Items, backupSchemaName, refBase)} }}" : string.Empty;
                    return $"\"type\": \"array\"{itemsProp}";
                case TDValues.TypeString:
                    string formatProp = TDValues.FormatValues.Contains(tdSchema.Value.Format?.Value.Value ?? string.Empty) ? $", \"format\": \"{tdSchema.Value.Format!.Value.Value}\"" :
                        tdSchema.Value.Pattern?.Value != null && Regex.IsMatch(Iso8601DurationExample, tdSchema.Value.Pattern.Value.Value) && !Regex.IsMatch(AnArbitraryString, tdSchema.Value.Pattern.Value.Value) ? @", ""format"": ""duration""" : string.Empty;
                    string patternProp = tdSchema.Value.Pattern?.Value != null && Regex.IsMatch(DecimalExample, tdSchema.Value.Pattern.Value.Value) && !Regex.IsMatch(AnArbitraryString, tdSchema.Value.Pattern.Value.Value) ? $", \"pattern\": \"{DecimalPattern}\"" : string.Empty;
                    string encodingProp = tdSchema.Value.ContentEncoding?.Value.Value == TDValues.ContentEncodingBase64 ? @", ""contentEncoding"": ""base64""" : string.Empty;
                    string enumProp = tdSchema.Value.Enum != null ? $", \"enum\": [ {string.Join(", ", $"\"{tdSchema.Value.Enum}\"")} ]" : string.Empty;
                    return $"\"type\": \"string\"{formatProp}{patternProp}{encodingProp}{enumProp}";
                case TDValues.TypeNumber:
                    string numberFormat = tdSchema.Value.Minimum?.Value.Value >= -3.40e+38 && tdSchema.Value.Maximum?.Value.Value <= 3.40e+38 ? "float" : "double";
                    return $"\"type\": \"number\", \"format\": \"{numberFormat}\"";
                case TDValues.TypeInteger:
                    string minProp = tdSchema.Value.Minimum?.Value != null ? $", \"minimum\": {(long)tdSchema.Value.Minimum.Value.Value}" : string.Empty;
                    string maxProp = tdSchema.Value.Maximum?.Value != null ? $", \"maximum\": {(long)tdSchema.Value.Maximum.Value.Value}" : string.Empty;
                    return $"\"type\": \"integer\"{minProp}{maxProp}";
                case TDValues.TypeBoolean:
                    return @"""type"": ""boolean""";
            }

            return @"""type"": ""null""";
        }
    }
}
