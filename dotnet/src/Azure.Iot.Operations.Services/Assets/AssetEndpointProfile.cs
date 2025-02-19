// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// The connection-level details to use when connecting with an asset endpoint.
    /// </summary>
    public record AssetEndpointProfile
    {
        internal AssetEndpointProfile(string targetAddress, string? authenticationMethod, string endpointProfileType)
        {
            TargetAddress = targetAddress;
            AuthenticationMethod = authenticationMethod;
            EndpointProfileType = endpointProfileType;
        }

        /// <summary>
        /// The address of the asset endpoint to connect to.
        /// </summary>
        public string TargetAddress { get; set; }
        
        /// <summary>
        /// The authentication method to use when connecting to the asset endpoint.
        /// </summary>
        public string? AuthenticationMethod { get; set; }

        /// <summary>
        /// The profile type of the asset endpoint.
        /// </summary>
        public string EndpointProfileType { get; set; }

        /// <summary>
        /// Optional application-layer configurations to reference when communicating with the asset endpoint.
        /// </summary>
        public JsonDocument? AdditionalConfiguration { get; set; }

        /// <summary>
        /// The credentials to use when connecting to the asset endpoint.
        /// </summary>
        /// <remarks>
        /// May be null if no credentials are required to connect to this asset endpoint.
        /// </remarks>
        public AssetEndpointProfileCredentials? Credentials { get; set; }
    }
}
