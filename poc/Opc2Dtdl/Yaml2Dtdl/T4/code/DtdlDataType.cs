namespace Yaml2Dtdl
{
    using System;
    using OpcUaDigest;

    public class DtdlDataType
    {
        private readonly DtdlObject? dtdlObject;
        private readonly DtdlEnum? dtdlEnum;

        public DtdlDataType(string modelId, OpcUaDataType dataType, TypeConverter typeConverter)
        {
            switch (dataType)
            {
                case OpcUaObj objType:
                    this.dtdlObject = new DtdlObject(modelId, objType, typeConverter);
                    break;
                case OpcUaEnum enumType:
                    this.dtdlEnum = new DtdlEnum(modelId, enumType);
                    break;
                default:
                    throw new NotSupportedException($"Cannot create {nameof(DtdlDataType)} from object of type {dataType.GetType()}");
            };
        }

        public string TransformText()
        {
            if (this.dtdlObject != null)
            {
                return this.dtdlObject.TransformText();
            }
            else if (this.dtdlEnum != null)
            {
                return this.dtdlEnum.TransformText();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
