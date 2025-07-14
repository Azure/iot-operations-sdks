namespace Yaml2Dtdl
{
    using OpcUaDigest;
    using SpecMapper;

    public partial class DtdlObject
    {
        private SpecMapper specMapper;
        private string modelId;
        private OpcUaObj objType;
        private TypeConverter typeConverter;

        public DtdlObject(SpecMapper specMapper, string modelId, OpcUaObj objType, TypeConverter typeConverter)
        {
            this.specMapper = specMapper;
            this.modelId = modelId;
            this.objType = objType;
            this.typeConverter = typeConverter;
        }
    }
}
