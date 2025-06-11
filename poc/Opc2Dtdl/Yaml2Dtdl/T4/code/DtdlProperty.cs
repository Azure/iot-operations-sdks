namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using OpcUaDigest;

    public class DtdlProperty : DtdlPropTelem
    {
        public DtdlProperty(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict)
            : base("Property", modelId, definedType, typeConverter, unitTypesDict, canBeWritable: true)
        {
        }
    }
}
