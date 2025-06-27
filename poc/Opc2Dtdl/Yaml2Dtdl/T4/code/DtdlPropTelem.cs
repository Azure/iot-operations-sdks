namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlPropTelem
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

        private string classType;
        private string modelId;
        private OpcUaDefinedType definedType;
        private TypeConverter typeConverter;
        private bool? isWritable;
        private bool isHistorized;
        private Dictionary<int, (string, string)> unitTypesDict;

        public string? DataType { get; }

        public List<OpcUaDefinedType> SubVars { get; }

        public DtdlPropTelem(string classType, string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict, bool canBeWritable)
        {
            this.classType = classType;
            this.modelId = modelId;
            this.definedType = definedType;

            this.DataType = definedType.Datatype;
            this.SubVars = GetSubVars();

            this.typeConverter = typeConverter;
            this.unitTypesDict = unitTypesDict;

            this.isWritable = canBeWritable ? (definedType.AccessLevel & writeMask) != 0 : null;
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

            OpcUaDefinedType specificDefinedType = definedType ?? this.definedType;

            OpcUaContent? engUnitContent = specificDefinedType.Contents.FirstOrDefault(c => c.Relationship == "HasProperty" && c.DefinedType.UnitId != null);
            if (engUnitContent?.DefinedType?.UnitId != null && int.TryParse(engUnitContent.DefinedType.UnitId, out int unitId) && unitTypesDict.TryGetValue(unitId, out unitInfo))
            {
                if (unitInfo.Item1 == "RelativeMeasure")
                {
                    unitInfo.Item1 = specificDefinedType.BrowseName switch
                    {
                        string n when n.Contains("Humidity") => "RelativeHumidity",
                        string n when n.Contains("Density") => "RelativeDensity",
                        string n when n.Contains("Size") => "Scale",
                        string n when n.Contains("Mixing") => "Concentration",
                        string n when n.Contains("Content") => "Concentration",
                        string n when n.Contains("Regulation") => "Throttle",
                        string n when n.Contains("Efficiency") => "Efficiency",
                        string n when n.Contains("PowerFactor") => "Efficiency",
                        _ => "RelativeMeasure",
                    };
                }

                return true;
            }
            else
            {
                unitInfo = default;
                return false;
            }
        }
    }
}
