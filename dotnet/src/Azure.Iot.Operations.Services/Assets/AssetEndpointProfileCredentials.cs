// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// The credentials to use when connecting to an asset endpoint.
    /// </summary>
    public record AssetEndpointProfileCredentials
    {
        internal AssetEndpointProfileCredentials(string? username, byte[]? password, string? certificate)
        {
            Username = username;
            Password = password;
            Certificate = certificate;
        }

        /// <summary>
        /// The x509 certificate to use for authentication when connecting with the asset endpoint.
        /// </summary>
        /// <remarks>
        /// This may be null if no x509 certificate is required for authentication when connecting to the asset endpoint.
        /// </remarks>
        public string? Certificate { get; private set; }

        /// <summary>
        /// The username to use for authentication when connecting with the asset endpoint.
        /// </summary>
        /// <remarks>
        /// This may be null if no username is required for authentication when connecting to the asset endpoint.
        /// </remarks>
        public string? Username { get; private set; }

        /// <summary>
        /// The password to use for authentication when connecting with the asset endpoint.
        /// </summary>
        /// <remarks>
        /// This may be null if no password is required for authentication when connecting to the asset endpoint.
        /// </remarks>
        public byte[]? Password { get; private set; }
    }
}
