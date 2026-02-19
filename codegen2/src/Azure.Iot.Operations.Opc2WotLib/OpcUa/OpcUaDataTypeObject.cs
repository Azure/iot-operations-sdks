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

            foreach (XmlNode childNode in definitionNode.ChildNodes)
            {
                if (childNode.Name == "Field")
                {
                    string? name = childNode.Attributes?["Name"]?.Value;
                    ArgumentNullException.ThrowIfNullOrEmpty(name, nameof(name));

                    string? dataTypeString = childNode.Attributes?["DataType"]?.Value;
                    OpcUaNodeId? dataTypeNodeId = dataTypeString != null ? UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap) : null;

                    string? symbolicName = childNode.Attributes?["SymbolicName"]?.Value;
                    int valueRank = int.Parse(childNode.Attributes?["ValueRank"]?.Value ?? "0");
                    bool isOptional = childNode.Attributes?["IsOptional"]?.Value == "true";

                    string? description = childNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Description")?.InnerText.CleanText();

                    ObjectFields[name] = new OpcUaObjectField(dataTypeNodeId, symbolicName, valueRank, isOptional, description);
                }
            }
        }

        public Dictionary<string, OpcUaObjectField> ObjectFields { get; }
    }
}
