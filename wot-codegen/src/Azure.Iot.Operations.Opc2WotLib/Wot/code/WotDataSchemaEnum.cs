// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public partial class WotDataSchemaEnum : WotDataSchema
    {
        private string typeRef;
        private string? description;
        private Dictionary<string, OpcUaEnumValue> enumValues;

        public WotDataSchemaEnum(OpcUaDataTypeEnum dataTypeEnum)
        {
            this.typeRef = dataTypeEnum.GetTypeRef();
            this.description = dataTypeEnum.Description;
            this.enumValues = dataTypeEnum.EnumValues;
        }
    }
}
