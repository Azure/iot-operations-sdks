/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public enum DatasetTarget
    {
        [EnumMember(Value = @"BrokerStateStore")]
        BrokerStateStore = 0,
        [EnumMember(Value = @"Mqtt")]
        Mqtt = 1,
        [EnumMember(Value = @"Storage")]
        Storage = 2,
    }
}
