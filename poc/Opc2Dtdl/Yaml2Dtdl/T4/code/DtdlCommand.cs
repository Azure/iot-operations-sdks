namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlCommand
    {
        private string modelId;
        private OpcUaDefinedType definedType;
        private TypeConverter typeConverter;

        public Dictionary<string, (string?, int)>? InputArgs { get; }

        public Dictionary<string, (string?, int)>? OutputArgs { get; }

        public bool InputIsOptional { get; }

        public bool OutputIsOptional { get; }

        public DtdlCommand(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter)
        {
            this.modelId = modelId;
            this.definedType = definedType;

            this.InputArgs = GetArguments("InputArguments");
            this.OutputArgs = GetArguments("OutputArguments");

            this.InputIsOptional = GetIsOptional("InputArguments");
            this.OutputIsOptional = GetIsOptional("OutputArguments");

            this.typeConverter = typeConverter;
        }

        private Dictionary<string, (string?, int)>? GetArguments(string browseName) => definedType.Contents.FirstOrDefault(
            c => c.Relationship == "HasProperty" &&
            c.DefinedType.BrowseName.EndsWith(browseName) &&
            c.DefinedType.Arguments.Count > 0)?.DefinedType?.Arguments;

        private bool GetIsOptional(string browseName) => definedType.Contents.Any(
            c => c.Relationship == "HasProperty" &&
            c.DefinedType.BrowseName == browseName &&
            c.DefinedType.Contents.Any(cc => cc.Relationship == "HasModellingRule" && cc.DefinedType.NodeId == TypeConverter.ModelingRuleOptionalNodeId));
    }
}
