using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt
{
    /// <summary>
    /// The options for an <see cref="OrderedAckMqttClient"/>.
    /// </summary>
    public class OrderedAckMqttClientOptions
    {
        /// <summary>
        /// Sets whether or not to use AIO broker-specific features. By default, this is true.
        /// </summary>
        public bool EnableAIOBrokerFeatures { get; set; } = true;
    }
}
