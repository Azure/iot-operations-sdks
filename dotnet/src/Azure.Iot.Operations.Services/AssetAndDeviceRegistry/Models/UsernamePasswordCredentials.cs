namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    
    public partial class UsernamePasswordCredentials : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The name of the secret containing the password.
        /// </summary>
        [JsonPropertyName("passwordSecretName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string PasswordSecretName { get; set; } = default!;

        /// <summary>
        /// The name of the secret containing the username.
        /// </summary>
        [JsonPropertyName("usernameSecretName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string UsernameSecretName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (PasswordSecretName is null)
            {
                throw new ArgumentNullException("passwordSecretName field cannot be null");
            }
            if (UsernameSecretName is null)
            {
                throw new ArgumentNullException("usernameSecretName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (PasswordSecretName is null)
            {
                throw new ArgumentNullException("passwordSecretName field cannot be null");
            }
            if (UsernameSecretName is null)
            {
                throw new ArgumentNullException("usernameSecretName field cannot be null");
            }
        }
    }
}
