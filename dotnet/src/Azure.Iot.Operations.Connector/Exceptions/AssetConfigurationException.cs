// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector.Exceptions
{
    /// <summary>
    /// An exception thrown when a connector fails to forward a sampled dataset or forward a received 
    /// event because of a missing or malformed configuration.
    /// </summary>
    /// <remarks>
    /// For example, this exception is thrown if a dataset or event has no configured MQTT topic to publish to.
    /// </remarks>
    public class AssetConfigurationException : Exception
    {
        public AssetConfigurationException()
        {
        }

        public AssetConfigurationException(string? message) : base(message)
        {
        }

        public AssetConfigurationException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
