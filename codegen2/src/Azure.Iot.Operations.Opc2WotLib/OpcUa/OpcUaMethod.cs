// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Xml;

    public class OpcUaMethod : OpcUaNode
    {
        public OpcUaMethod(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode methodNode)
            : base(modelInfo, nsUriToNsInfoMap, methodNode)
        {
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, methodNode);
        }

        public bool TryGetInputArguments([NotNullWhen(true)] out OpcUaVariable? inputArgsVariable)
        {
            inputArgsVariable = Properties.OfType<OpcUaVariable>().FirstOrDefault(v => v.BrowseName.Name == "InputArguments");
            return inputArgsVariable != null;
        }

        public bool TryGetOutputArguments([NotNullWhen(true)] out OpcUaVariable? outputArgsVariable)
        {
            outputArgsVariable = Properties.OfType<OpcUaVariable>().FirstOrDefault(v => v.BrowseName.Name == "OutputArguments");
            return outputArgsVariable != null;
        }
    }
}
