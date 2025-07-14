namespace Yaml2Dtdl
{
    using OpcUaDigest;
    using SpecMapper;

    public partial class DtdlEnum
    {
        private SpecMapper specMapper;
        private string modelId;
        private OpcUaEnum enumType;

        public DtdlEnum(SpecMapper specMapper, string modelId, OpcUaEnum enumType)
        {
            this.specMapper = specMapper;
            this.modelId = modelId;
            this.enumType = enumType;
        }
    }
}
