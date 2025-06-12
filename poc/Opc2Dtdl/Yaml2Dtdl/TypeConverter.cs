namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using OpcUaDigest;
    using SpecMapper;

    public class TypeConverter
    {
        public const string ModelingRuleOptionalNodeId = "80";

        private const string coreSpecName = "OpcUaCore";
        private const string defaultType = "string";

        // Per https://reference.opcfoundation.org/Core/Part6/v104/docs/5
        // and https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v4/DTDL.v4.md#primitive-schema
        //
        private static readonly Dictionary<string, string> builtInTypeMap = new Dictionary<string, string>
        {
            { "Boolean" , "boolean" },
            { "SByte" , "byte" },
            { "Byte" , "unsignedByte" },
            { "Int16" , "short" },
            { "UInt16" , "unsignedShort" },
            { "Int32" , "integer" },
            { "UInt32" , "unsignedInteger" },
            { "Int64" , "long" },
            { "UInt64" , "unsignedLong" },
            { "Float" , "float" },
            { "Double" , "double" },
            { "String" , "string" },
            { "DateTime" , "dateTime" },
            { "Guid" , "uuid" },
            { "ByteString" , "bytes" },
            { "XmlElement" , defaultType },
            { "NodeId" , defaultType },
            { "ExpandedNodeId" , defaultType },
            { "DiagnosticInfo" , "bytes" },
            { "QualifiedName" , defaultType },
            { "LocalizedText" , "string" },
            { "StatusCode" , "integer" },
            { "Number" , "integer" },
            { "Integer" , "integer" },
            { "UInteger" , "unsignedInteger" },
            { "ExtensionObject" , "bytes" },
            { "Variant" , defaultType },
            { "DataValue" , "bytes" },
            { "Decimal" , "scaledDecimal" },
            { "DecimalString" , "decimal" },
            { "RelativePath", defaultType },
            { "EUInformation", defaultType },
            { "UriString", "string" },
            { "Duration", "duration" },
            { "UtcTime", "time" },
            { "BaseDataType", defaultType },
            { "Structure", "bytes" },
        };

        private Dictionary<string, string> localTypeMap = new();

        public void SetLocalTypeMap(Dictionary<string, string> localTypeMap)
        {
            this.localTypeMap = localTypeMap;
        }

        public string GetDtdlTypeFromOpcUaType(string modelId, string? opcUaType, int arrayDimensions, string propertyName, (string, string) unitInfo, int indent)
        {
            if (!propertyName.Contains('<'))
            {
                return this.GetDtdlTypeFromOpcUaType(modelId, opcUaType, arrayDimensions, indent);
            }

            string legalizedName = LegalizeName(StripAngles(Dequalify(propertyName)));

            string valueSchema = this.GetDtdlTypeFromOpcUaType(modelId, opcUaType, arrayDimensions, indent + 4);
            string it = new string(' ', indent);

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine($"{it}  \"@type\": \"Map\",");
            stringBuilder.AppendLine($"{it}  \"mapKey\": {{");
            stringBuilder.AppendLine($"{it}    \"name\": \"{legalizedName}\",");
            stringBuilder.AppendLine($"{it}    \"schema\": \"string\"");
            stringBuilder.AppendLine($"{it}  }},");

            stringBuilder.AppendLine($"{it}  \"mapValue\": {{");
            if (unitInfo.Item1 != string.Empty)
            {
                stringBuilder.AppendLine($"{it}    \"@type\": [ \"MapValue\", \"{unitInfo.Item1}\" ],");
            }
            stringBuilder.AppendLine($"{it}    \"name\": \"{legalizedName}Schema\",");
            if (unitInfo.Item2 != string.Empty)
            {
                stringBuilder.AppendLine($"{it}    \"unit\": \"{unitInfo.Item2}\",");
            }
            stringBuilder.AppendLine($"{it}    \"schema\": {valueSchema}");
            stringBuilder.AppendLine($"{it}  }}");
            stringBuilder.Append($"{it}}}");

            return stringBuilder.ToString();
        }

        public string GetDtdlTypeFromOpcUaType(string modelId, string? opcUaType, int arrayDimensions, int indent)
        {
            if (arrayDimensions <= 0)
            {
                return this.GetDtdlTypeFromOpcUaType(modelId, opcUaType);
            }

            string elementSchema = this.GetDtdlTypeFromOpcUaType(modelId, opcUaType, arrayDimensions - 1, indent + 2);
            string it = new string(' ', indent);

            StringBuilder stringBuilder = new ();
            stringBuilder.AppendLine("{");
            stringBuilder.AppendLine($"{it}  \"@type\": \"Array\",");
            stringBuilder.AppendLine($"{it}  \"elementSchema\": {elementSchema}");
            stringBuilder.Append($"{it}}}");

            return stringBuilder.ToString();
        }

        public string GetDtdlTypeFromOpcUaType(string modelId, string? opcUaType)
        {
            if (opcUaType == null)
            {
                return $"\"{defaultType}\"";
            }

            string nextType = opcUaType;
            while (true)
            {
                if (builtInTypeMap.TryGetValue(nextType, out string? dtdlType))
                {
                    return $"\"{dtdlType}\"";
                }
                else if (localTypeMap.TryGetValue(nextType, out string? mappedType))
                {
                    nextType = mappedType;
                }
                else
                {
                    return $"\"{GetDataTypeDtmiFromBrowseName(modelId, nextType)}\"";
                }
            }
        }

        public static string GetTypeRefFromNodeId(string nodeId)
        {
            int sepIx = nodeId.IndexOf(':');
            string? specName = sepIx < 0 ? null : nodeId.Substring(0, sepIx);
            string idNum = nodeId.Substring(sepIx + 1);
            string uri = SpecMapper.GetUriFromSpecName(specName);
            return $"nsu={uri};i={idNum}";
        }

        public static HashSet<string> BuiltInTypes { get => builtInTypeMap.Keys.ToHashSet(); }

        public static string LegalizeName(string browseName) => (char.IsNumber(browseName[0]) ? "X_" : "") + Regex.Replace(browseName, "[^a-zA-Z0-9]+", "_", RegexOptions.CultureInvariant);

        public static string StripAngles(string browseName) => browseName.Replace("<", string.Empty).Replace(">", string.Empty).Replace(".", string.Empty);

        public static string GetDataTypeDtmiFromBrowseName(string modelId, string browseName) => $"{modelId}:dataType:{LegalizeName(Dequalify(browseName))};1";

        public static string GetModelId(OpcUaDefinedType definedType) => $"dtmi:opcua:{GetSpecName(definedType)}:{Dequalify(definedType.BrowseName)}".Replace('.', '_').Replace('-', '_');

        public static string Dequalify(string browseName) => browseName.Substring(browseName.IndexOf(':') + 1);

        public static string? GetSpecName(OpcUaDefinedType definedType) => GetSpecName(definedType.BrowseName) ?? GetSpecName(definedType.NodeId) ?? coreSpecName;

        public static string? GetSpecName(string nameOrId)
        {
            int sepIx = nameOrId.IndexOf(':');
            return sepIx < 0 ? null : nameOrId.Substring(0, sepIx);
        }
    }
}
