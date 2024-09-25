﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    /// <summary>
    /// The credentials to use when connecting to an asset endpoint.
    /// </summary>
    public record AssetEndpointProfileCredentials
    {
        internal AssetEndpointProfileCredentials(string? username, byte[]? password, X509Certificate2? certificate)
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
        public X509Certificate2? Certificate { get; private set; }

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
