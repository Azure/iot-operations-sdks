namespace Azure.Iot.Operations.ProtocolCompiler
{
    public record CommandSchemaInfo(string Name, ITypeName? RequestSchema, ITypeName? ResponseSchema, CodeName? NormalResultName, CodeName? NormalResultSchema, CodeName? ErrorResultName, CodeName? ErrorResultSchema, bool RequestNullable, bool ResponseNullable, bool Idempotent, string? Ttl);
}
