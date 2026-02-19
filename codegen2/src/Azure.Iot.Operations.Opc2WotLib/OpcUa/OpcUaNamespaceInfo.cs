// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public class OpcUaNamespaceInfo
    {
        public OpcUaNamespaceInfo()
        {
            NodeIndexToNodeMap = new Dictionary<int, OpcUaNode>();
            NodeStringToNodeIndexMap = new Dictionary<string, int>();
            NextNodeIndexForNodeString = -1;
        }

        public Dictionary<int, OpcUaNode> NodeIndexToNodeMap { get; }

        public Dictionary<string, int> NodeStringToNodeIndexMap { get; }

        public int NextNodeIndexForNodeString { get; set; }
    }
}
