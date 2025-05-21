namespace Common
{
    public static class Constants
    {
        public const string MqttNamespace = "MyMqttNamespace";

        public const string MappingFilePath = "config/DeviceToMqttMapping.json";

        public const string QuantitativeTypeMapFilePath = "config/QuantitativeTypeMap.json";

        public const string MappingPropertyDeviceAddress = "deviceAddress";

        public const string MappingPropertyCommFormat = "commFormat";

        public const string MappingPropertyDeviceTypeId = "deviceTypeId";

        public const string AlphaDeviceAddress = "DataSourceAlpha";

        public const string AlphaDeviceType = "BareBonesDevice";

        public const string BetaDeviceAddress = "DataSourceBeta";

        public const string BetaDeviceType = "DeluxeDevice";

        public const string CommFormatJson = "JSON";

        public const string CommFormatCsv = "CSV";
    }
}
