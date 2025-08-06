namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public record ErrorSchemaInfo(CodeName Schema, string? Description, CodeName? MessageField, bool IsNullable, CodeName? CodeName, CodeName? CodeSchema, CodeName? InfoName, CodeName? InfoSchema)
    {
        public ErrorSchemaInfo(CodeName schema, string? description, (CodeName?, bool, CodeName?, CodeName?, CodeName?, CodeName?) messageFieldInfo)
            : this(schema, description, messageFieldInfo.Item1, messageFieldInfo.Item2, messageFieldInfo.Item3, messageFieldInfo.Item4, messageFieldInfo.Item5, messageFieldInfo.Item6)
        {
        }
    }
}
