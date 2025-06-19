namespace Yaml2Dtdl
{
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlRelationship
    {
        private OpcUaDefinedType definedType;
        private bool isPlaceholder;

        public string? Target { get; }

        public DtdlRelationship(OpcUaDefinedType definedType)
        {
            this.definedType = definedType;
            this.isPlaceholder = IsPlaceholder();

            this.Target = GetTarget();
        }

        private string? GetTarget()
        {
            OpcUaDefinedType? typeDefinition = definedType.Contents.FirstOrDefault(c => c.Relationship == "HasTypeDefinition")?.DefinedType;
            if (typeDefinition == null)
            {
                return null;
            }

            return TypeConverter.GetModelId(typeDefinition);
        }

        private bool IsPlaceholder() => definedType.Contents.Any(c => c.Relationship == "HasModellingRule" &&
            (c.DefinedType.NodeId == TypeConverter.ModelingRuleOptionalPlaceholderNodeId || c.DefinedType.NodeId == TypeConverter.ModelingRuleMandatoryPlaceholderNodeId));
    }
}
