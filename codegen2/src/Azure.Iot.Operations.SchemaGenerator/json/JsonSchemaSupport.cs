namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.IO;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal class JsonSchemaSupport
    {
        private const string Iso8601DurationExample = "P3Y6M4DT12H30M5S";
        private const string DecimalExample = "1234567890.0987654321";
        private const string AnArbitraryString = "HelloWorld";
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
            return Path.GetRelativePath(this.workingDir.FullName, Path.Combine(refBase, reference)).Replace('\\', '/');
        }

        internal string GetTypeAndAddenda(TDDataSchema tdSchema, string backupSchemaName, string refBase)
        {
            if (tdSchema.Ref != null)
            {
                return $"\"$ref\": \"{GetReferencePath(tdSchema.Ref, refBase)}\"";
            }

            if ((tdSchema.Type == TDValues.TypeObject && tdSchema.AdditionalProperties?.Boolean == false) ||
                (tdSchema.Type == TDValues.TypeString && tdSchema.Enum != null))
            {
                return $"\"$ref\": \"{this.schemaNamer.ApplyBackupSchemaName(tdSchema.Title, backupSchemaName)}.json\"";
            }

            switch (tdSchema.Type ?? string.Empty)
            {
                case TDValues.TypeObject:
                    return $"\"type\": \"object\", \"additionalProperties\": {{ {GetTypeAndAddenda(tdSchema.AdditionalProperties!.DataSchema!, backupSchemaName, refBase)} }}";
                case TDValues.TypeArray:
                    string itemsProp = tdSchema.Items != null ? $", \"items\": {{ {GetTypeAndAddenda(tdSchema.Items, backupSchemaName, refBase)} }}" : string.Empty;
                    return $"\"type\": \"array\"{itemsProp}";
                case TDValues.TypeString:
                    string formatProp = TDValues.FormatValues.Contains(tdSchema.Format ?? string.Empty) ? $", \"format\": \"{tdSchema.Format}\"" :
                        tdSchema.Pattern != null && Regex.IsMatch(Iso8601DurationExample, tdSchema.Pattern) && !Regex.IsMatch(AnArbitraryString, tdSchema.Pattern) ? @", ""format"": ""duration""" : string.Empty;
                    string patternProp = tdSchema.Pattern != null && Regex.IsMatch(DecimalExample, tdSchema.Pattern) && !Regex.IsMatch(AnArbitraryString, tdSchema.Pattern) ? $", \"pattern\": \"{DecimalPattern}\"" : string.Empty;
                    string encodingProp = tdSchema.ContentEncoding == TDValues.ContentEncodingBase64 ? @", ""contentEncoding"": ""base64""" : string.Empty;
                    string enumProp = tdSchema.Enum != null ? $", \"enum\": [ {string.Join(", ", $"\"{tdSchema.Enum}\"")} ]" : string.Empty;
                    return $"\"type\": \"string\"{formatProp}{patternProp}{encodingProp}{enumProp}";
                case TDValues.TypeNumber:
                    string numberFormat = tdSchema.Minimum >= -3.40e+38 && tdSchema.Maximum <= 3.40e+38 ? "float" : "double";
                    return $"\"type\": \"number\", \"format\": \"{numberFormat}\"";
                case TDValues.TypeInteger:
                    string minProp = tdSchema.Minimum != null ? $", \"minimum\": {(long)tdSchema.Minimum}" : string.Empty;
                    string maxProp = tdSchema.Maximum != null ? $", \"maximum\": {(long)tdSchema.Maximum}" : string.Empty;
                    return $"\"type\": \"integer\"{minProp}{maxProp}";
                case TDValues.TypeBoolean:
                    return @"""type"": ""boolean""";
            }

            return @"""type"": ""null""";
        }
    }
}
