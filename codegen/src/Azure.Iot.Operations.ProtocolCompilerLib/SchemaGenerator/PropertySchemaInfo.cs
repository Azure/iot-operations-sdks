namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public record PropertySchemaInfo(
        string? Name,
        CodeName PropSchema,
        ITypeName ReadRespSchema,
        ITypeName? WriteReqSchema,
        ITypeName? WriteRespSchema,
        CodeName? PropValueName,
        CodeName? ReadErrorName,
        CodeName? ReadErrorSchema,
        CodeName? WriteErrorName,
        CodeName? WriteErrorSchema);
}
