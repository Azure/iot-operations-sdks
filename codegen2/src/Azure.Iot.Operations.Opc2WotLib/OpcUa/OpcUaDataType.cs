// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Xml;

    public abstract class OpcUaDataType : OpcUaNode
    {
        protected OpcUaDataType(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, dataTypeNode)
        {
        }

        public static bool TryCreate(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode, out OpcUaDataType? dataType)
        {
            XmlNode? someFieldNode = dataTypeNode.SelectSingleNode("descendant::opc:Field", OpcUaGraph.NamespaceManager);

            if (someFieldNode?.Attributes!["Value"] != null)
            {
                dataType = new OpcUaDataTypeEnum(modelInfo, nsUriToNsInfoMap, dataTypeNode);
                return true;
            }
            else if (someFieldNode?.Attributes!["DataType"] != null)
            {
                dataType = new OpcUaDataTypeObject(modelInfo, nsUriToNsInfoMap, dataTypeNode);
                return true;
            }
            else
            {
                dataType = new OpcUaDataTypeSubtype(modelInfo, nsUriToNsInfoMap, dataTypeNode);
                return true;
            }
        }
    }
}
