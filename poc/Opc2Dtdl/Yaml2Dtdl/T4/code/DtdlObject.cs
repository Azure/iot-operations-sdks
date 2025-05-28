namespace Yaml2Dtdl
{
    using OpcUaDigest;
    public partial class DtdlObject
    {
        private string modelId;
        private OpcUaObj objType;
        private TypeConverter typeConverter;

        public DtdlObject(string modelId, OpcUaObj objType, TypeConverter typeConverter)
        {
            this.modelId = modelId;
            this.objType = objType;
            this.typeConverter = typeConverter;
        }
    }
}
