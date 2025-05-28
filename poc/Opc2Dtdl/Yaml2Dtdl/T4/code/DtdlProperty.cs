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

        public (string?, int) Variable { get; }

        public Dictionary<string, (string?, int)> SubVariables { get; }

        public DtdlProperty(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter)
        {
            this.modelId = modelId;
            this.definedType = definedType;

            this.Variable = ( definedType.Datatype, definedType.ValueRank );
            this.SubVariables = GetSubVariables();

            this.typeConverter = typeConverter;

            this.isWritable = (definedType.AccessLevel & writeMask) != 0;
            this.isHistorized = (definedType.AccessLevel & historyMask) != 0;
        }

        private Dictionary<string, (string?, int)> GetSubVariables() => definedType.Contents
            .Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAVariable")
            .ToDictionary(c => c.DefinedType.BrowseName, c => (c.DefinedType.Datatype, c.DefinedType.ValueRank));
    }
}
