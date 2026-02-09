// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaVariable : OpcUaNode
    {
        public OpcUaVariable(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode variableNode)
            : base(modelInfo, nsUriToNsInfoMap, variableNode)
        {
            string? dataTypeString = variableNode.Attributes?["DataType"]?.Value;
            if (dataTypeString != null)
            {
                DataType = UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap);
            }

            ValueRank = int.Max(int.Parse(variableNode.Attributes?["ValueRank"]?.Value ?? "0"), 0);
            AccessLevel = int.Parse(variableNode.Attributes?["AccessLevel"]?.Value ?? "0");
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, variableNode);
            Arguments = GetArgumentsFromXmlNode(modelInfo, nsUriToNsInfoMap, variableNode);

            IsPlaceholder = References.Any(r => 
                r.IsForward &&
                r.ReferenceType.IsModellingRuleReference &&
                r.Target.IsRulePlaceholder);
        }

        public OpcUaNodeId? DataType { get; }

        public int ValueRank { get; }

        public int AccessLevel { get; }

        public Dictionary<string, OpcUaObjectField> Arguments { get; }

        public bool IsPlaceholder { get; }

        private static Dictionary<string, OpcUaObjectField> GetArgumentsFromXmlNode(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode xmlNode)
        {
            Dictionary<string, OpcUaObjectField> arguments = new Dictionary<string, OpcUaObjectField>();

            XmlNodeList? argNodeList = xmlNode.SelectNodes("descendant::uax:Argument", OpcUaGraph.NamespaceManager);
            if (argNodeList == null)
            {
                return arguments;
            }

            foreach (XmlNode argNode in argNodeList)
            {
                string? argName = argNode.SelectSingleNode("child::uax:Name", OpcUaGraph.NamespaceManager)?.InnerText;
                if (argName != null)
                {
                    string? dataTypeString = argNode.SelectSingleNode("child::uax:DataType/child::uax:Identifier", OpcUaGraph.NamespaceManager)?.InnerText;
                    OpcUaNodeId? dataType = dataTypeString != null ? UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap) : null;
                    int valueRank = int.Parse(argNode.SelectSingleNode("child::uax:ValueRank", OpcUaGraph.NamespaceManager)?.InnerText ?? "0");
                    string? description = argNode.SelectSingleNode("child::uax:Description/child::uax:Text", OpcUaGraph.NamespaceManager)?.InnerText.CleanText();
                    arguments[argName] = new OpcUaObjectField(dataType, null, valueRank, false, description);
                }
            }

            return arguments;
        }
    }
}
