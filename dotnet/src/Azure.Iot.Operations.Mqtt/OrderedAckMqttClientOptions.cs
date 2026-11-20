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

        /// <summary>
        /// The value of the <c>metriccategory</c> MQTT CONNECT user property, which categorizes this
        /// client's traffic to AIO services. Only sent when <see cref="EnableAIOBrokerFeatures"/> is true.
        /// </summary>
        /// <remarks>
        /// See <see href="https://learn.microsoft.com/azure/iot-operations/reference/observability-metrics-mqtt-broker#category"/> for details.
        /// </remarks>
        public string MetricCategory { get; set; } = "aiosdk-dotnet";
    }
}
