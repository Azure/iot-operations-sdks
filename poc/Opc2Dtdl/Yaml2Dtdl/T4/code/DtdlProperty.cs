namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlProperty
    {
        // bits defined here: https://reference.opcfoundation.org/Core/Part3/v104/docs/8.57
        private const int bitPosWrite = 1;
        private const int bitPosHistory = 2;

        private readonly int writeMask = 1 << bitPosWrite;
        private readonly int historyMask = 1 << bitPosHistory;

        private string modelId;
        private OpcUaDefinedType definedType;
        private TypeConverter typeConverter;
        private bool isWritable;
        private bool isHistorized;

        public string? DataType { get; }

        public List<OpcUaDefinedType> SubVars { get; }

        public DtdlProperty(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter)
        {
            this.modelId = modelId;
            this.definedType = definedType;

            this.DataType = definedType.Datatype;
            this.SubVars = GetSubVars();

            this.typeConverter = typeConverter;

            this.isWritable = (definedType.AccessLevel & writeMask) != 0;
            this.isHistorized = (definedType.AccessLevel & historyMask) != 0;
        }

        private List<OpcUaDefinedType> GetSubVars() => definedType.Contents
            .Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAVariable").Select(c => c.DefinedType).ToList();

        private bool GetIsOptional(OpcUaDefinedType definedType) =>
            definedType.Contents.Any(c => c.Relationship == "HasModellingRule" && c.DefinedType.NodeId == TypeConverter.ModelingRuleOptionalNodeId);
    }
}
