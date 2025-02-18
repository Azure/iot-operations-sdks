// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// An exception that indicates a failure to sample a dataset due to that asset no longer being available to sample.
    /// </summary>
    public class AssetDatasetUnavailableException : ConnectorException
    {
        public AssetDatasetUnavailableException()
        {
        }

        public AssetDatasetUnavailableException(string? message) : base(message)
        {
        }

        public AssetDatasetUnavailableException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
