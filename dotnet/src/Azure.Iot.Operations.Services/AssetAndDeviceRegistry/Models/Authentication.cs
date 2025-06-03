namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class Authentication
    {
        /// <summary>
        /// Defines the method to authenticate the user of the client at the server.
        /// </summary>
        [JsonPropertyName("method")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public Method Method { get; set; } = default!;

        /// <summary>
        /// The credentials for authentication mode UsernamePassword.
        /// </summary>
        [JsonPropertyName("usernamePasswordCredentials")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public UsernamePasswordCredentials? UsernamePasswordCredentials { get; set; } = default;

        /// <summary>
        /// The x509 certificate for authentication mode Certificate.
        /// </summary>
        [JsonPropertyName("x509Credentials")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public X509credentials? X509credentials { get; set; } = default;

    }
}
