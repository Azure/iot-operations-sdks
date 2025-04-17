/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class X509credentialsSchema : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// The 'certificateSecretName' Field.
        /// </summary>
        [JsonPropertyName("certificateSecretName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string CertificateSecretName { get; set; } = default!;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (CertificateSecretName is null)
            {
                throw new ArgumentNullException("certificateSecretName field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (CertificateSecretName is null)
            {
                throw new ArgumentNullException("certificateSecretName field cannot be null");
            }
        }
    }
}
