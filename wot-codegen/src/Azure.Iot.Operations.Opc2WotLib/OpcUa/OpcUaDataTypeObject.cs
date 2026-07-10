// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaDataTypeObject : OpcUaDataType
    {
        public OpcUaDataTypeObject(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, dataTypeNode)
        {
            ObjectFields = new Dictionary<string, OpcUaObjectField>();

            XmlNode? definitionNode = dataTypeNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Definition");
            ArgumentNullException.ThrowIfNull(definitionNode, nameof(definitionNode));

            IsUnion = definitionNode.Attributes?["IsUnion"]?.Value == "true";

            foreach (XmlNode childNode in definitionNode.ChildNodes)
            {
                if (childNode.Name == "Field" && childNode.Attributes?["ReleaseStatus"]?.Value != "Deprecated")
                {
                    string? name = childNode.Attributes?["Name"]?.Value;
                    ArgumentNullException.ThrowIfNullOrEmpty(name, nameof(name));

                    string? dataTypeString = childNode.Attributes?["DataType"]?.Value;
                    OpcUaNodeId? dataTypeNodeId = dataTypeString != null ? UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap) : null;

                    string? symbolicName = childNode.Attributes?["SymbolicName"]?.Value;
                    int valueRank = int.Parse(childNode.Attributes?["ValueRank"]?.Value ?? "0");
                    bool isOptional = childNode.Attributes?["IsOptional"]?.Value == "true";

                    string? description = childNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Description")?.InnerText.CleanText();

                    ObjectFields[name] = new OpcUaObjectField(this, dataTypeNodeId, symbolicName, valueRank, isOptional, description);
                }
            }
        }

        public Dictionary<string, OpcUaObjectField> ObjectFields { get; }

        public bool IsUnion { get; }

        public Dictionary<string, OpcUaObjectField> GetAllObjectFields()
        {
            Dictionary<string, OpcUaObjectField> fields = new();
            CollateObjectFields(this, fields, new HashSet<OpcUaDataType>());
            return fields;
        }

        private static void CollateObjectFields(OpcUaDataType dataType, Dictionary<string, OpcUaObjectField> fields, HashSet<OpcUaDataType> processedDataTypes)
        {
            if (!processedDataTypes.Add(dataType))
            {
                throw new InvalidOperationException($"Cycle detected in the base-type chain for OPC UA DataType '{dataType.EffectiveName}'.");
            }

            foreach (OpcUaNodeId baseTypeNodeId in dataType.BaseTypes.Where(nodeId => !nodeId.IsBuiltInDataType))
            {
                if (dataType.GetReferencedOpcUaNode(baseTypeNodeId) is OpcUaDataType baseDataType)
                {
                    CollateObjectFields(baseDataType, fields, processedDataTypes);
                }
            }

            if (dataType is OpcUaDataTypeObject objectDataType)
            {
                foreach (KeyValuePair<string, OpcUaObjectField> field in objectDataType.ObjectFields)
                {
                    if (!fields.TryAdd(field.Key, field.Value))
                    {
                        throw new InvalidOperationException($"OPC UA DataType '{objectDataType.EffectiveName}' redeclares inherited field '{field.Key}'.");
                    }
                }
            }

            processedDataTypes.Remove(dataType);
        }
    }
}
