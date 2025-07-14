namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using OpcUaDigest;
    using SpecMapper;

    public class DtdlProperty : DtdlPropTelem
    {
        public DtdlProperty(SpecMapper specMapper, string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict)
            : base(specMapper, "Property", modelId, definedType, typeConverter, unitTypesDict, canBeWritable: true)
        {
        }
    }
}
