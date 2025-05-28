namespace Yaml2Dtdl
{
    using OpcUaDigest;

    public partial class DtdlEnum
    {
        private string modelId;
        private OpcUaEnum enumType;

        public DtdlEnum(string modelId, OpcUaEnum enumType)
        {
            this.modelId = modelId;
            this.enumType = enumType;
        }
    }
}
