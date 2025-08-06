namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public record CommandEnvoyInfo(
        CodeName Name,
        ITypeName? RequestSchema,
        ITypeName? ResponseSchema,
        CodeName? NormalResultName,
        CodeName? NormalResultSchema,
        CodeName? ErrorResultName,
        CodeName? ErrorResultSchema,
        CodeName? ErrorCodeName,
        CodeName? ErrorCodeSchema,
        CodeName? ErrorInfoName,
        CodeName? ErrorInfoSchema,
        bool RequestNullable,
        bool ResponseNullable);
}
