// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaMethod : OpcUaNode
    {
        public OpcUaMethod(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode methodNode)
            : base(modelInfo, nsUriToNsInfoMap, methodNode)
        {
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, methodNode);
        }
    }
}
