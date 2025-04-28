// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    public class DeviceCredentials
    {
        /// <summary>
        /// The username to use when connecting to this device with username/password authorization. May be null if no username/password authorization should be used.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// The password to use when connecting to this device with username/password authorization. May be null if no username/password authorization should be used.
        /// </summary>
        public byte[]? Password { get; set; }

        /// <summary>
        /// The client certificate to use when connecting to this device with x509 authorization. May be null if no x509 authorization should be used.
        /// </summary>
        public string? ClientCertificate { get; set; }

        /// <summary>
        /// The device's CA certificate to use when connecting to this device with TLS authentication.
        /// </summary>
        public string? CaCertificate { get; set; }
    }
}
