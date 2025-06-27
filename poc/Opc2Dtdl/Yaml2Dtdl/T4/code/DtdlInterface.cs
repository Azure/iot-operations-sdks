namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlInterface
    {
        private string modelId;
        private bool isComposite;
        private bool isEvent;
        private string topicBase;
        private OpcUaDefinedType definedType;
        private TypeConverter typeConverter;
        private HashSet<string> supertypeIds;
        private List<DtdlProperty> dtdlProperties;
        private List<DtdlTelemetry> dtdlTelemetries;
        private List<DtdlCommand> dtdlCommands;
        private List<DtdlRelationship> dtdlRelationships;
        private List<DtdlDataType> dtdlDataTypes;
        private int contentCount;
        private bool appendComma;

        public DtdlInterface(string modelId, bool isComposite, bool isEvent, OpcUaDefinedType definedType, List<OpcUaDataType> dataTypes, List<OpcUaDataType> coreDataTypes, Dictionary<int, (string, string)> unitTypesDict, CotypeRuleEngine cotypeRuleEngine, bool appendComma)
        {
            this.modelId = modelId;
            this.isComposite = isComposite;
            this.isEvent = isEvent;
            this.topicBase = modelId.Substring("dtmi:".Length).Replace(':', '/');
            this.definedType = definedType;
            this.typeConverter = new();

            this.supertypeIds = new (definedType.Contents.Where(c => c.Relationship == "HasSubtype_reverse" && c.DefinedType.NodeType == "UAObjectType").Select(c => TypeConverter.GetModelId(c.DefinedType)));
            this.dtdlProperties = definedType.Contents.Where(c => c.Relationship == "HasProperty" && c.DefinedType.NodeType == "UAVariable").Select(c => new DtdlProperty(modelId, c.DefinedType, this.typeConverter, unitTypesDict)).ToList();
            this.dtdlTelemetries = definedType.Contents.Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAVariable").Select(c => new DtdlTelemetry(modelId, c.DefinedType, this.typeConverter, unitTypesDict)).ToList();
            this.dtdlCommands = definedType.Contents.Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAMethod").Select(c => new DtdlCommand(modelId, c.DefinedType, this.typeConverter)).ToList();
            this.dtdlRelationships = definedType.Contents.Where(c => c.Relationship == "HasComponent" && c.DefinedType.NodeType == "UAObject").Select(c => new DtdlRelationship(definedType, c.DefinedType, cotypeRuleEngine)).ToList();
            this.contentCount = dtdlProperties.Count + dtdlTelemetries.Count + dtdlCommands.Count + dtdlRelationships.Count;

            this.appendComma = appendComma;

            HashSet<string> pendingTypeStrings = new();
            AddPropertyReferences(pendingTypeStrings);
            AddTelemetryReferences(pendingTypeStrings);
            AddCommandReferences(pendingTypeStrings);
            List<OpcUaDataType> referencedDataTypes = GetReferencedDataTypes(pendingTypeStrings, dataTypes, coreDataTypes);

            this.dtdlDataTypes = referencedDataTypes.Where(dt => dt is OpcUaObj || dt is OpcUaEnum).Select(dt => new DtdlDataType(modelId, dt, this.typeConverter)).ToList();
            Dictionary<string, string> localTypeMap = referencedDataTypes.Where(dt => dt is OpcUaSub).ToDictionary(dt => dt.BrowseName, dt => ((OpcUaSub)dt).Bases.First());
            this.typeConverter.SetLocalTypeMap(localTypeMap);
        }

        private List<OpcUaDataType> GetReferencedDataTypes(HashSet<string> pendingTypeStrings, List<OpcUaDataType> dataTypes, List<OpcUaDataType> coreDataTypes)
        {
            HashSet<string> processedTypeStrings = new();
            List<OpcUaDataType> referencedDataTypes = new();
            while (pendingTypeStrings.Count > 0)
            {
                string pendingType = pendingTypeStrings.First();

                OpcUaDataType? dataType =
                    dataTypes.Any(dt => dt.BrowseName == pendingType) ? dataTypes.First(dt => dt.BrowseName == pendingType) :
                    coreDataTypes.FirstOrDefault(dt => dt.BrowseName == pendingType);
                if (dataType != null)
                {
                    referencedDataTypes.Add(dataType);

                    switch (dataType)
                    {
                        case OpcUaSub subType:
                            foreach (string baseType in subType.Bases)
                            {
                                if (!processedTypeStrings.Contains(baseType))
                                {
                                    AddIfNotBuiltIn(pendingTypeStrings, baseType);
                                }
                            }
                            break;
                        case OpcUaObj objType:
                            foreach (KeyValuePair<string, (string?, int)> field in objType.Fields)
                            {
                                if (field.Value.Item1 != null && !processedTypeStrings.Contains(field.Value.Item1))
                                {
                                    AddIfNotBuiltIn(pendingTypeStrings, field.Value.Item1);
                                }
                            }
                            break;
                    }
                }

                processedTypeStrings.Add(pendingType);
                pendingTypeStrings.Remove(pendingType);
            }

            return referencedDataTypes;
        }

        private void AddPropertyReferences(HashSet<string> pendingTypeStrings)
        {
            foreach (DtdlProperty dtdlProperty in this.dtdlProperties)
            {
                AddIfNotBuiltIn(pendingTypeStrings, dtdlProperty.DataType);
                foreach (OpcUaDefinedType definedType in dtdlProperty.SubVars)
                {
                    AddIfNotBuiltIn(pendingTypeStrings, definedType.Datatype);
                }
            }
        }

        private void AddTelemetryReferences(HashSet<string> pendingTypeStrings)
        {
            foreach (DtdlTelemetry dtdlTelemetry in this.dtdlTelemetries)
            {
                AddIfNotBuiltIn(pendingTypeStrings, dtdlTelemetry.DataType);
                foreach (OpcUaDefinedType definedType in dtdlTelemetry.SubVars)
                {
                    AddIfNotBuiltIn(pendingTypeStrings, definedType.Datatype);
                }
            }
        }

        private void AddCommandReferences(HashSet<string> pendingTypeStrings)
        {
            foreach (DtdlCommand dtdlCommand in this.dtdlCommands)
            {
                if (dtdlCommand.InputArgs != null)
                {
                    foreach (KeyValuePair<string, (string?, int)> inputArg in dtdlCommand.InputArgs)
                    {
                        AddIfNotBuiltIn(pendingTypeStrings, inputArg.Value.Item1);
                    }
                }

                if (dtdlCommand.OutputArgs != null)
                {
                    foreach (KeyValuePair<string, (string?, int)> outputArg in dtdlCommand.OutputArgs)
                    {
                        AddIfNotBuiltIn(pendingTypeStrings, outputArg.Value.Item1);
                    }
                }
            }
        }

        private void AddIfNotBuiltIn(HashSet<string> types, string? type)
        {
            if (type != null && !TypeConverter.BuiltInTypes.Contains(type))
            {
                types.Add(type);
            }
        }
    }
}
