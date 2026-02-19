// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaDataTypeEnum : OpcUaDataType
    {
        public OpcUaDataTypeEnum(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, dataTypeNode)
        {
            EnumValues = new Dictionary<string, OpcUaEnumValue>();

            XmlNode? definitionNode = dataTypeNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Definition");
            ArgumentNullException.ThrowIfNull(definitionNode, nameof(definitionNode));

            foreach (XmlNode childNode in definitionNode.ChildNodes)
            {
                if (childNode.Name == "Field")
                {
                    string? name = childNode.Attributes?["Name"]?.Value;
                    ArgumentNullException.ThrowIfNullOrEmpty(name, nameof(name));

                    string? symbolicName = childNode.Attributes?["SymbolicName"]?.Value;
                    int value = int.Parse(childNode.Attributes?["Value"]?.Value ?? "0");

                    string? description = childNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Description")?.InnerText.CleanText();
 
                    EnumValues[name] = new OpcUaEnumValue(value, symbolicName, description);
                }
            }
        }

        public Dictionary<string, OpcUaEnumValue> EnumValues { get; }
    }
}
