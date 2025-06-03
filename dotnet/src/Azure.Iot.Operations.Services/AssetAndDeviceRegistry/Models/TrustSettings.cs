namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class TrustSettings
    {
        /// <summary>
        /// Secret reference to the issuers list to trust.
        /// </summary>
        [JsonPropertyName("issuerList")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? IssuerList { get; set; } = default;

        /// <summary>
        /// Secret reference to certificates list to trust.
        /// </summary>
        [JsonPropertyName("trustList")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? TrustList { get; set; } = default;

    }
}
