namespace Yaml2Dtdl
{
    using System.Collections.Generic;
    using OpcUaDigest;

    public class DtdlTelemetry : DtdlPropTelem
    {
        public DtdlTelemetry(string modelId, OpcUaDefinedType definedType, TypeConverter typeConverter, Dictionary<int, (string, string)> unitTypesDict)
            : base("Telemetry", modelId, definedType, typeConverter, unitTypesDict, canBeWritable: false)
        {
        }
    }
}
