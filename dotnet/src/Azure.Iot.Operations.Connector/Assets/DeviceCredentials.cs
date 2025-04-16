// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.Assets
{
    public class DeviceCredentials
    {
        public string? Username { get; set; }

        public byte[]? Password { get; set; }

        public string? ClientCertificate { get; set; }

        public string? CaCertificate { get; set; }
    }
}
