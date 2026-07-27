// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaVariableType : OpcUaNode
    {
        private List<OpcUaVariableType>? baseTypes;

        public OpcUaVariableType(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode variableTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, variableTypeNode)
        {
            string? dataTypeString = variableTypeNode.Attributes?["DataType"]?.Value;
            if (!string.IsNullOrEmpty(dataTypeString))
            {
                DataType = UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap);
            }

            string? valueRankString = variableTypeNode.Attributes?["ValueRank"]?.Value;
            DeclaredValueRank = valueRankString != null ? int.Parse(valueRankString) : null;
            IsAbstract = variableTypeNode.Attributes?["IsAbstract"]?.Value == "true";
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, variableTypeNode);
        }

        public OpcUaNodeId? DataType { get; }

        public int? DeclaredValueRank { get; }

        public bool IsAbstract { get; }

        public List<OpcUaVariableType> BaseTypes
        {
            get
            {
                baseTypes ??= References
                    .Where(r => !r.IsForward && r.ReferenceType.IsSubtypeReference)
                    .Select(r => GetReferencedOpcUaNode(r.Target))
                    .Cast<OpcUaVariableType>()
                    .ToList();

                return baseTypes;
            }
        }

        public OpcUaNodeId? EffectiveDataType => EffectiveDataTypeSource?.DataType;

        public OpcUaVariableType? EffectiveDataTypeSource => ResolveDataTypeSource(new HashSet<OpcUaVariableType>());

        public int EffectiveValueRank => int.Max(ResolveValueRank(new HashSet<OpcUaVariableType>()) ?? 0, 0);

        private OpcUaVariableType? ResolveDataTypeSource(HashSet<OpcUaVariableType> ancestors)
        {
            if (DataType != null)
            {
                return this;
            }

            if (!ancestors.Add(this))
            {
                throw new InvalidOperationException($"VariableType inheritance cycle detected at '{GetTypeRef()}'.");
            }

            foreach (OpcUaVariableType baseType in BaseTypes)
            {
                OpcUaVariableType? dataTypeSource = baseType.ResolveDataTypeSource(ancestors);
                if (dataTypeSource != null)
                {
                    ancestors.Remove(this);
                    return dataTypeSource;
                }
            }

            ancestors.Remove(this);
            return null;
        }

        private int? ResolveValueRank(HashSet<OpcUaVariableType> ancestors)
        {
            if (DeclaredValueRank != null)
            {
                return DeclaredValueRank;
            }

            if (!ancestors.Add(this))
            {
                throw new InvalidOperationException($"VariableType inheritance cycle detected at '{GetTypeRef()}'.");
            }

            foreach (OpcUaVariableType baseType in BaseTypes)
            {
                int? valueRank = baseType.ResolveValueRank(ancestors);
                if (valueRank != null)
                {
                    ancestors.Remove(this);
                    return valueRank;
                }
            }

            ancestors.Remove(this);
            return null;
        }
    }
}
