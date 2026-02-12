// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;

    public static class EnumExtractor
    {
        public static List<OpcUaDataTypeEnum> ExtractEnums(this OpcUaObjectType uaObjectType)
        {
            List<OpcUaDataType> dataTypes = ExtractDataTypesFromObjectType(uaObjectType);

            Dictionary<OpcUaNodeId, OpcUaDataTypeEnum> dataTypeEnums = new();
            HashSet<OpcUaNodeId> processedNodeIds = new();

            foreach (OpcUaDataType dataType in dataTypes)
            {
                CollateDataTypeEnums(dataType, dataTypeEnums, processedNodeIds);
            }

            return dataTypeEnums.Values.ToList();
        }

        private static List<OpcUaDataType> ExtractDataTypesFromObjectType(OpcUaObjectType uaObjectType)
        {
            Dictionary<OpcUaNodeId, OpcUaDataType> dataTypes = new();

            foreach (UaVariableRecord variableRecord in uaObjectType.VariableRecords.Values.Where(r => r.UaVariable.DataType != null))
            {
                dataTypes[variableRecord.UaVariable.DataType!] = (OpcUaDataType)variableRecord.UaVariable.GetReferencedOpcUaNode(variableRecord.UaVariable.DataType!);
            }

            foreach (OpcUaMethod method in uaObjectType.Methods)
            {
                if (method.TryGetInputArguments(out OpcUaVariable? inputArgsVariable))
                {
                    foreach (OpcUaObjectField argField in inputArgsVariable.Arguments.Values)
                    {
                        if (argField.DataType != null)
                        {
                            dataTypes[argField.DataType] = (OpcUaDataType)inputArgsVariable.GetReferencedOpcUaNode(argField.DataType);
                        }
                    }
                }

                if (method.TryGetOutputArguments(out OpcUaVariable? outputArgsVariable))
                {
                    foreach (OpcUaObjectField argField in outputArgsVariable.Arguments.Values)
                    {
                        if (argField.DataType != null)
                        {
                            dataTypes[argField.DataType] = (OpcUaDataType)outputArgsVariable.GetReferencedOpcUaNode(argField.DataType);
                        }
                    }
                }
            }

            return dataTypes.Values.ToList();
        }

        private static void CollateDataTypeEnums(OpcUaDataType dataType, Dictionary<OpcUaNodeId, OpcUaDataTypeEnum> dataTypeEnums, HashSet<OpcUaNodeId> processedNodeIds)
        {
            if (!processedNodeIds.Add(dataType.NodeId))
            {
                return;
            }

            switch (dataType)
            {
                case OpcUaDataTypeEnum dataTypeEnum:
                    dataTypeEnums[dataType.NodeId] = dataTypeEnum;
                    break;
                case OpcUaDataTypeObject dataTypeObject:
                    foreach (OpcUaObjectField field in dataTypeObject.ObjectFields.Values.Where(f => f.DataType != null))
                    {
                        CollateDataTypeEnums((OpcUaDataType)dataTypeObject.GetReferencedOpcUaNode(field.DataType!), dataTypeEnums, processedNodeIds);
                    }
                    break;
                case OpcUaDataTypeSubtype dataTypeSubtype:
                    foreach (OpcUaNodeId baseTypeId in dataTypeSubtype.BaseTypes)
                    {
                        CollateDataTypeEnums((OpcUaDataType)dataTypeSubtype.GetReferencedOpcUaNode(baseTypeId), dataTypeEnums, processedNodeIds);
                    }
                    break;
            }
        }
    }
}
