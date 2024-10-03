namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.SchemaExtractor
{
    using System.Collections.Generic;

    public class EnumTypeInfo : SchemaTypeInfo
    {
        public EnumTypeInfo(string schemaName, List<string> enumNames)
            : base(schemaName)
        {
            EnumNames = enumNames.ToArray();
        }

        public string[] EnumNames { get; set; }
    }
}
