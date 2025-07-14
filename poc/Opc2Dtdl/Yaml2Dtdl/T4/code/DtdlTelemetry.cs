namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using OpcUaDigest;
    using SpecMapper;

    public class DtdlTelemetry : DtdlPropTelem
    {
        public DtdlTelemetry(SpecMapper specMapper, string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict)
            : base(specMapper, "Telemetry", modelId, definedType, typeConverter, unitTypesDict, canBeWritable: false)
        {
        }
    }
}
