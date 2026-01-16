namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public record PropertySpec(
        CodeName Name,
        CodeName Consumer,
        CodeName Maintainer,
        CodeName PropSchema,
        EmptyTypeName ReadSerializerEmptyType,
        EmptyTypeName WriteSerializerEmptyType,
        ITypeName? ReadRespSchema,
        ITypeName? WriteReqSchema,
        ITypeName? WriteRespSchema,
        CodeName? PropValueName,
        CodeName? ReadErrorName,
        CodeName? ReadErrorSchema,
        CodeName? WriteErrorName,
        CodeName? WriteErrorSchema,
        bool DoesReadTargetMaintainer,
        bool DoesWriteTargetMaintainer,
        bool IsAggregate)
    {
        public PropertySpec(
            SchemaNamer schemaNamer,
            string propertyName,
            string propSchema,
            SerializationFormat readFormat,
            SerializationFormat writeFormat,
            string? readRespSchema,
            string? writeReqSchema,
            string? writeRespSchema,
            string? propValueName,
            string? readErrorName,
            string? readErrorSchema,
            string? writeErrorName,
            string? writeErrorSchema,
            bool doesReadTargetMaintainer,
            bool doesWriteTargetMaintainer,
            bool isAggregate)
            : this(
                new CodeName(propertyName),
                new CodeName(schemaNamer.GetPropConsumerBinder(propSchema)),
                new CodeName(schemaNamer.GetPropMaintainerBinder(propSchema)),
                new CodeName(propSchema),
                readFormat.GetEmptyTypeName(),
                writeFormat.GetEmptyTypeName(),
                readRespSchema != null ? new CodeName(readRespSchema) : null,
                writeReqSchema != null ? new CodeName(writeReqSchema) : null,
                writeRespSchema != null ? new CodeName(writeRespSchema) : null,
                propValueName != null ? new CodeName(propValueName) : null,
                readErrorName != null ? new CodeName(readErrorName) : null,
                readErrorSchema != null ? new CodeName(readErrorSchema) : null,
                writeErrorName != null ? new CodeName(writeErrorName) : null,
                writeErrorSchema != null ? new CodeName(writeErrorSchema) : null,
                doesReadTargetMaintainer,
                doesWriteTargetMaintainer,
                isAggregate)
        {
        }
    }
}
