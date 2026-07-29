// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaObject : OpcUaNode
    {
        private OpcUaObjectType? hasTypeDefinition = null;

        public OpcUaObject(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode objectNode)
            : base(modelInfo, nsUriToNsInfoMap, objectNode)
        {
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, objectNode);

            XmlNode? referencesNode = objectNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "References" && node.Attributes?["ReleaseStatus"]?.Value != "Deprecated");
            if (referencesNode != null)
            {
                XmlNode? hasTypeDefinitionNode = referencesNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Reference" && node.Attributes?["ReferenceType"]?.Value == "HasTypeDefinition" && node.Attributes?["ReleaseStatus"]?.Value != "Deprecated");
                if (hasTypeDefinitionNode != null)
                {
                    string? hasTypeDefinitionNodeIdString = hasTypeDefinitionNode.InnerText;
                    if (hasTypeDefinitionNodeIdString != null)
                    {
                        HasTypeDefinitionNodeId = UaUtil.ParseTypeString(hasTypeDefinitionNodeIdString, modelInfo, nsUriToNsInfoMap);
                    }
                }

                XmlNode? hasModellingRuleNode = referencesNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Reference" && node.Attributes?["ReferenceType"]?.Value == "HasModellingRule" && node.Attributes?["ReleaseStatus"]?.Value != "Deprecated");
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

        public OpcUaObjectType? HasTypeDefinition
        {
            get
            {
                if (hasTypeDefinition == null && HasTypeDefinitionNodeId != null)
                {
                    hasTypeDefinition = GetReferencedOpcUaNode(HasTypeDefinitionNodeId) as OpcUaObjectType;
                }

                return hasTypeDefinition;
            }
        }

        public OpcUaNodeId? HasTypeDefinitionNodeId { get; }

        public OpcUaNodeId? HasModellingRule { get; }
    }
}
