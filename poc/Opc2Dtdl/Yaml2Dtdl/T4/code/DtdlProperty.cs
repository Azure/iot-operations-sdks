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

        private static readonly int writeMask = 1 << bitPosWrite;
        private static readonly int historyMask = 1 << bitPosHistory;

        private static readonly HashSet<string> numericTypes = new HashSet<string>
        {
            "SByte",
            "Byte",
            "Int16",
            "UInt16",
            "Int32",
            "UInt32",
            "Int64",
            "UInt64",
            "Float",
            "Double",
            "Number",
            "Integer",
            "UInteger",
            "Decimal",
        };

        private string modelId;
        private OpcUaDefinedType definedType;
        private TypeConverter typeConverter;
        private bool isWritable;
        private bool isHistorized;
        private Dictionary<int, (string, string)> unitTypesDict;

        public string? DataType { get; }

        public List<OpcUaDefinedType> SubVars { get; }

        public DtdlProperty(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict)
        {
            this.modelId = modelId;
            this.definedType = definedType;

            this.DataType = definedType.Datatype;
            this.SubVars = GetSubVars();

            this.typeConverter = typeConverter;
            this.unitTypesDict = unitTypesDict;

            this.isWritable = (definedType.AccessLevel & writeMask) != 0;
            this.isHistorized = (definedType.AccessLevel & historyMask) != 0;
        }

        private List<OpcUaDefinedType> GetSubVars() => definedType.Contents
            .Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAVariable").Select(c => c.DefinedType).ToList();

        private bool IsOptional(OpcUaDefinedType definedType) =>
            definedType.Contents.Any(c => c.Relationship == "HasModellingRule" && c.DefinedType.NodeId == TypeConverter.ModelingRuleOptionalNodeId);

        private string GetCotypes(OpcUaDefinedType? definedType)
        {
            List<string> cotypes = new ();

            cotypes.Add("Qualified");

            if (definedType == null && this.isHistorized)
            {
                cotypes.Add("Historized");
            }

            if (definedType != null && !IsOptional(definedType))
            {
                cotypes.Add("Required");
            }

            if (TryGetUnitInfo(definedType, out (string, string)  unitInfo) && !this.definedType.BrowseName.Contains('<'))
            {
                cotypes.Add(unitInfo.Item1);
            }

            return string.Join(", ", cotypes.Select(c => $"\"{c}\""));
        }

        private bool TryGetUnitInfo(OpcUaDefinedType? definedType, out (string, string) unitInfo)
        {
            if (definedType == null && this.SubVars.Count > 0)
            {
                unitInfo = default;
                return false;
            }

            if (!numericTypes.Contains((definedType ?? this.definedType).Datatype!))
            {
                unitInfo = default;
                return false;
            }

            OpcUaContent? engUnitContent = (definedType ?? this.definedType).Contents.FirstOrDefault(c => c.Relationship == "HasProperty" && c.DefinedType.UnitId != null);
            if (engUnitContent?.DefinedType?.UnitId != null && int.TryParse(engUnitContent.DefinedType.UnitId, out int unitId))
            {
                return unitTypesDict.TryGetValue(unitId, out unitInfo);
            }
            else
            {
                unitInfo = default;
                return false;
            }
        }
    }
}
