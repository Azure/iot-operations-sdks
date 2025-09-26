namespace Azure.Iot.Operations.SchemaGenerator
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal record EnumSpec(string? Description, List<string> Values, SerializationFormat Format, string SchemaName) : SchemaSpec(Format)
    {
        internal static EnumSpec CreateFromDataSchema(TDDataSchema dataSchema, SerializationFormat format, string backupName, string? defaultDescription = null)
        {
            string schemaName = dataSchema.Title ?? backupName;

            if (dataSchema.Type != TDValues.TypeString || dataSchema.Enum == null)
            {
                throw new Exception($"Cannot create enum spec from schema definition with type {dataSchema.Type ?? "unspecfied"} or with no enum values.");
            }

            return new EnumSpec(dataSchema.Description ?? defaultDescription, dataSchema.Enum.ToList(), format, schemaName);
        }
    }
}
