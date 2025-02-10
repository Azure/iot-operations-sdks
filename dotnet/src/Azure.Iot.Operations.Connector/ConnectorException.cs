// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// The generic exception to indicate something went wrong when running a connector. It may be releated to connecting to the asset, 
    /// reading/writing data to the asset, or reading/writing data to the MQTT broker.
    /// </summary>
    /// <seealso cref="AssetDatasetUnavailableException"/>
    /// <seealso cref="AssetSamplingException"/>
    public class ConnectorException : Exception
    {
        public ConnectorException()
        {
        }

        public ConnectorException(string? message) : base(message)
        {
        }

        public ConnectorException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
