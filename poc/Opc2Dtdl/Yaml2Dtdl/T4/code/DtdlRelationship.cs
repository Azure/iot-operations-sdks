namespace Yaml2Dtdl
{
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlRelationship
    {
        private const string capabilityCotype = @", ""HasCapability""";
        private const string componentCotype = @", ""HasComponent""";
        private const string folderType = "dtmi:opcua:OpcUaCore:FolderType";
        private const string baseObjectType = "dtmi:opcua:OpcUaCore:BaseObjectType";

        private OpcUaDefinedType definedType;

        public string? Target { get; }

        public DtdlRelationship(OpcUaDefinedType definedType)
        {
            this.definedType = definedType;

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

        private bool HasModellingRule(string modellingRuleNodeId) => definedType.Contents.Any(c => c.Relationship == "HasModellingRule" && c.DefinedType.NodeId == modellingRuleNodeId);

        private bool IsPlaceholder() => HasModellingRule(TypeConverter.ModelingRuleOptionalPlaceholderNodeId) || HasModellingRule(TypeConverter.ModelingRuleMandatoryPlaceholderNodeId);

        private string GetCotype()
        {
            if (IsPlaceholder() || Target == folderType)
            {
                return componentCotype;
            }
            else if (HasModellingRule(TypeConverter.ModelingRuleMandatoryNodeId) && Target != baseObjectType)
            {
                return capabilityCotype;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
