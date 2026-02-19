// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaDataTypeSubtype : OpcUaDataType
    {
        public OpcUaDataTypeSubtype(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, dataTypeNode)
        {
            BaseTypes = new List<OpcUaNodeId>();

            XmlNode? referencesNode = dataTypeNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "References");
            if (referencesNode == null)
            {
                return;
            }

            foreach (XmlNode childNode in referencesNode.ChildNodes)
            {
                if (childNode.Name == "Reference")
                {
                    string? referenceType = childNode.Attributes?["ReferenceType"]?.Value;
                    string? isForward = childNode.Attributes?["IsForward"]?.Value;

                    if (referenceType == "HasSubtype" && isForward == "false")
                    {
                        string? baseTypeNodeIdString = childNode.InnerText;
                        if (baseTypeNodeIdString != null)
                        {
                            OpcUaNodeId baseTypeNodeId = UaUtil.ParseTypeString(baseTypeNodeIdString, modelInfo, nsUriToNsInfoMap);
                            BaseTypes.Add(baseTypeNodeId);
                        }
                    }
                }
            }
        }

        public List<OpcUaNodeId> BaseTypes { get; }
    }
}
