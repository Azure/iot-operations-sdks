// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Connector
{
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

        protected AssetDatasetUnavailableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
