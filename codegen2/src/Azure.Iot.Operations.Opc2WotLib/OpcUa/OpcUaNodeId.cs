// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public class OpcUaNodeId
    {
        private const int HasModellingRuleNodeIndex = 37;
        private const int HasSubtypeNodeIndex = 45;
        private const int HasPropertyNodeIndex = 46;
        private const int HasComponentNodeIndex = 47;

        public OpcUaNodeId(int nsIndex, int nodeIndex)
        {
            NsIndex = nsIndex;
            NodeIndex = nodeIndex;
        }

        public OpcUaNodeId(string nodeIdString, OpcUaModelInfo? modelInfo, Dictionary<string, OpcUaNamespaceInfo>? nsUriToNsInfoMap)
        {
            (NsIndex, NodeIndex) = UaUtil.ParseNodeIdString(nodeIdString, modelInfo, nsUriToNsInfoMap);
        }

        public int NsIndex { get; }

        public int NodeIndex { get; }

        public bool IsSubtypeReference { get => NsIndex == 0 && NodeIndex == HasSubtypeNodeIndex; }

        public bool IsModellingRuleReference { get => NsIndex == 0 && NodeIndex == HasModellingRuleNodeIndex; }

        public bool IsPropertyReference { get => NsIndex == 0 && NodeIndex == HasPropertyNodeIndex; }

        public bool IsComponentReference { get => NsIndex == 0 && NodeIndex == HasComponentNodeIndex; }
    }
}
