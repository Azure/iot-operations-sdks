namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal record SchemaSpec(SerializationFormat Format, long TokenIndex)
    {
        internal static bool TryCreateFromDataSchema(ErrorReporter errorReporter, SchemaNamer schemaNamer, ValueTracker<TDDataSchema> dataSchema, SerializationFormat format, string backupName, [NotNullWhen(true)] out SchemaSpec? schemaSpec)
        {
            if (dataSchema.Value.Type?.Value.Value == TDValues.TypeObject && dataSchema.Value.AdditionalProperties == null)
            {
                schemaSpec = ObjectSpec.CreateFromDataSchema(errorReporter, schemaNamer, dataSchema, format, backupName);
                return true;
            }
            else if (dataSchema.Value.Enum != null)
            {
                schemaSpec = EnumSpec.CreateFromDataSchema(errorReporter, schemaNamer, dataSchema, format, backupName);
                return true;
            }
            else
            {
                schemaSpec = null;
                return false;
            }
        }
    }
}
