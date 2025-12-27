namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Linq;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal record EnumSpec(string? Description, List<string> Values, SerializationFormat Format, string SchemaName, long TokenIndex) : SchemaSpec(Format, TokenIndex)
    {
        internal static EnumSpec CreateFromDataSchema(ErrorReporter errorReporter, SchemaNamer schemaNamer, ValueTracker<TDDataSchema> dataSchema, SerializationFormat format, string backupName, string? defaultDescription = null)
        {
            string schemaName = schemaNamer.ApplyBackupSchemaName(dataSchema.Value.Title?.Value.Value, backupName);

            if (dataSchema.Value.Type?.Value.Value != TDValues.TypeString)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"Enum schema '{schemaName}' must have type 'string'.", dataSchema.TokenIndex);
            }
            if (dataSchema.Value.Enum?.Elements == null)
            {
                errorReporter.ReportError(ErrorCondition.ElementMissing, $"Enum schema '{schemaName}' must have at least one defined value.", dataSchema.TokenIndex);
            }

            string? description = dataSchema.Value.Description?.Value.Value ?? defaultDescription;
            List<string> values = dataSchema.Value.Enum?.Elements != null ? dataSchema.Value.Enum.Elements.Select(e => e.Value.Value).ToList() : new();

            return new EnumSpec(description, values, format, schemaName, dataSchema.TokenIndex);
        }
    }
}
