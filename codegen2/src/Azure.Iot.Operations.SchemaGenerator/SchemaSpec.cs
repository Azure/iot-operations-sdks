namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal record SchemaSpec(SerializationFormat Format)
    {
        internal static bool TryCreateFromDataSchema(SchemaNamer schemaNamer, TDDataSchema dataSchema, SerializationFormat format, string backupName, [NotNullWhen(true)] out SchemaSpec? schemaSpec)
        {
            if (dataSchema.Type == TDValues.TypeObject && dataSchema.AdditionalProperties?.Boolean == false)
            {
                schemaSpec = ObjectSpec.CreateFromDataSchema(schemaNamer, dataSchema, format, backupName);
                return true;
            }
            else if (dataSchema.Type == TDValues.TypeString && dataSchema.Enum != null)
            {
                schemaSpec = EnumSpec.CreateFromDataSchema(schemaNamer, dataSchema, format, backupName);
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
