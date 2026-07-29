// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Xml;

    public class OpcUaDataTypeSubtype : OpcUaDataType
    {
        public OpcUaDataTypeSubtype(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode dataTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, dataTypeNode)
        {
        }
    }
}
