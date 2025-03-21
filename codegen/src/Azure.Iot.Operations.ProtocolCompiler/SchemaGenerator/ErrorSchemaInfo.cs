namespace Azure.Iot.Operations.ProtocolCompiler
{
    public record ErrorSchemaInfo(CodeName Schema, string? Description, CodeName? MessageField, bool IsNullable)
    {
        public ErrorSchemaInfo(CodeName schema, string? description, (CodeName?, bool) messageFieldInfo)
            : this(schema, description, messageFieldInfo.Item1, messageFieldInfo.Item2)
        {
        }
    }
}
