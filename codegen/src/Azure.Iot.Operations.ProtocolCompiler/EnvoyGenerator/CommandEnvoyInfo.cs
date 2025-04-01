namespace Azure.Iot.Operations.ProtocolCompiler
{
    public record CommandEnvoyInfo(CodeName Name, ITypeName? RequestSchema, ITypeName? ResponseSchema, CodeName? NormalResultName, CodeName? NormalResultSchema, CodeName? ErrorResultName, CodeName? ErrorResultSchema, bool RequestNullable, bool ResponseNullable);
}
