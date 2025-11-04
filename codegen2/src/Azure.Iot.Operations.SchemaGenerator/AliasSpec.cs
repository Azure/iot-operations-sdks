namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal record AliasSpec(string? Description, string Ref, SerializationFormat Format, string SchemaName, string Base) : SchemaSpec(Format);
}
