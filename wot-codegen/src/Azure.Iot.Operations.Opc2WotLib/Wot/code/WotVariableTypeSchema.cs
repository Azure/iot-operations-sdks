// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class WotVariableTypeSchema
    {
        public static Dictionary<OpcUaVariableType, string> GetSchemaNames(IEnumerable<OpcUaVariableType> variableTypes, IEnumerable<string>? reservedNames = null)
        {
            Dictionary<OpcUaVariableType, string> schemaNames = new();
            HashSet<string> usedNames = new(reservedNames ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

            foreach (OpcUaVariableType variableType in variableTypes
                .Distinct()
                .OrderBy(vt => vt.DefiningModel.ModelUri, StringComparer.Ordinal)
                .ThenBy(vt => vt.EffectiveName, StringComparer.Ordinal)
                .ThenBy(vt => vt.NodeId.NodeIndex))
            {
                string schemaName = WotUtil.LegalizeName(variableType.EffectiveName);
                if (!usedNames.Add(schemaName))
                {
                    string specName = SpecMapper.GetSpecNameFromUri(variableType.DefiningModel.ModelUri);
                    schemaName = WotUtil.LegalizeName(variableType.EffectiveName, specName);
                    if (!usedNames.Add(schemaName))
                    {
                        schemaName = $"{schemaName}_{variableType.NodeId.NodeIndex}";
                        if (!usedNames.Add(schemaName))
                        {
                            throw new InvalidOperationException($"Unable to assign a unique schema name for VariableType '{variableType.GetTypeRef()}'.");
                        }
                    }
                }

                schemaNames[variableType] = schemaName;
            }

            return schemaNames;
        }

        public static WotDataSchema Create(OpcUaVariableType variableType)
        {
            return WotDataSchema.Create(
                variableType.EffectiveDataType,
                variableType.EffectiveValueRank,
                variableType.EffectiveDataTypeSource ?? variableType,
                variableType.Description,
                Enumerable.Empty<OpcUaNodeId>(),
                variableType);
        }
    }
}
