namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public record TypedConstant(CodeName Name, string Type, object Value, string? Description);
}
