/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class GetDeviceResponsePayload : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The Command response argument.
        /// </summary>
        [JsonPropertyName("device")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Device Device { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (Device is null)
            {
                throw new ArgumentNullException("device field cannot be null");
            }
        }
    }
}
