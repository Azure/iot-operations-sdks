// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaObject : OpcUaNode
    {
        public OpcUaObject(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode objectNode)
            : base(modelInfo, nsUriToNsInfoMap, objectNode)
        {
            XmlNode? referencesNode = objectNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "References");
            if (referencesNode != null)
            {
                XmlNode? hasTypeDefinitionNode = referencesNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Reference" && node.Attributes?["ReferenceType"]?.Value == "HasTypeDefinition");
                if (hasTypeDefinitionNode != null)
                {
                    string? hasTypeDefinitionNodeIdString = hasTypeDefinitionNode.InnerText;
                    if (hasTypeDefinitionNodeIdString != null)
                    {
                        HasTypeDefinitionNodeId = UaUtil.ParseTypeString(hasTypeDefinitionNodeIdString, modelInfo, nsUriToNsInfoMap);
                    }
                }

                XmlNode? hasModellingRuleNode = referencesNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Reference" && node.Attributes?["ReferenceType"]?.Value == "HasModellingRule");
                if (hasModellingRuleNode != null)
                {
                    string? hasModellingRuleNodeIdString = hasModellingRuleNode.InnerText;
                    if (hasModellingRuleNodeIdString != null)
                    {
                        HasModellingRule = UaUtil.ParseTypeString(hasModellingRuleNodeIdString, modelInfo, nsUriToNsInfoMap);
                    }
                }
            }
        }

        public OpcUaNodeId? HasTypeDefinitionNodeId { get; }

        public OpcUaNodeId? HasModellingRule { get; }
    }
}
