// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;

    public class OpcUaNodeId : IEquatable<OpcUaNodeId>
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

        public bool IsRuleOptional { get => NsIndex == 0 && (NodeIndex == (int)ModellingRule.Optional || NodeIndex == (int)ModellingRule.OptionalPlaceholder); }

        public bool IsRuleMandatory { get => NsIndex == 0 && (NodeIndex == (int)ModellingRule.Mandatory || NodeIndex == (int)ModellingRule.MandatoryPlaceholder); }

        public bool IsRulePlaceholder { get => NsIndex == 0 && (NodeIndex == (int)ModellingRule.OptionalPlaceholder || NodeIndex == (int)ModellingRule.MandatoryPlaceholder); }

        public bool Equals(OpcUaNodeId? other)
        {
            return other != null && NsIndex == other.NsIndex && NodeIndex == other.NodeIndex;
        }

        public override int GetHashCode()
        {
            return (NsIndex, NodeIndex).GetHashCode();
        }
    }
}
