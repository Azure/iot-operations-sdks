namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public record TypedConstant(string Type, object Value, string? Description);
}
