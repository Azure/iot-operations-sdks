namespace Yaml2Dtdl
{
    using System.Linq;
    using OpcUaDigest;
    using SpecMapper;

    public partial class DtdlRelationship
    {
        private SpecMapper specMapper;
        private OpcUaDefinedType sourceDefinedType;
        private OpcUaDefinedType relationshipDefinedType;
        private OpcUaDefinedType? targetDefinedType;
        private CotypeRuleEngine cotypeRuleEngine;

        public string? Target { get; }

        public DtdlRelationship(SpecMapper specMapper, OpcUaDefinedType sourceDefinedType, OpcUaDefinedType relationshipDefinedType, CotypeRuleEngine cotypeRuleEngine)
        {
            this.specMapper = specMapper;
            this.sourceDefinedType = sourceDefinedType;
            this.relationshipDefinedType = relationshipDefinedType;
            this.targetDefinedType = relationshipDefinedType.Contents.FirstOrDefault(c => c.Relationship == "HasTypeDefinition")?.DefinedType;
            this.cotypeRuleEngine = cotypeRuleEngine;

            this.Target = this.targetDefinedType != null ? TypeConverter.GetModelId(this.targetDefinedType) : null;
        }

        private bool HasModellingRule(string modellingRuleNodeId) => relationshipDefinedType.Contents.Any(c => c.Relationship == "HasModellingRule" && c.DefinedType.NodeId == modellingRuleNodeId);

        private bool IsPlaceholder() => HasModellingRule(TypeConverter.ModelingRuleOptionalPlaceholderNodeId) || HasModellingRule(TypeConverter.ModelingRuleMandatoryPlaceholderNodeId);

        private string GetCotype()
        {
            string? cotype = cotypeRuleEngine.GetCotype(sourceDefinedType, relationshipDefinedType, targetDefinedType);

            return cotype != null ? $", \"{cotype}\"" : string.Empty;
        }
    }
}
