namespace Azure.Iot.Operations.EnvoyGenerator
{
    public record ErrorSpec(string SchemaName, string? ErrorCodeName, string? ErrorCodeSchema, string? ErrorInfoName, string? ErrorInfoSchema, string Description, string? MessageField, bool MessageIsRequired);
}
