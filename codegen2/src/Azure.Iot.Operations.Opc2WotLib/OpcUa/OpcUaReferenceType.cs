// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaReferenceType : OpcUaNode
    {
        public OpcUaReferenceType(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode referenceTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, referenceTypeNode)
        {
            IsSymmetric = referenceTypeNode.Attributes?["Symmetric"]?.Value == "true";
            XmlNode? inverseNameNode = referenceTypeNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "InverseName");
            InverseName = inverseNameNode?.InnerText;
        }

        public bool IsSymmetric { get; }

        public string? InverseName { get; }
    }
}
