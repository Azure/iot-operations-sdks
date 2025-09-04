namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public record PropertyEnvoyInfo(
        CodeName? Name,
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
