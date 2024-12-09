using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// An implementation of <see cref="ConnectorWorker"/> that publishes data read from an asset to the MQTT broker as telemetry.
    /// </summary>
    public class TelemetryConnectorWorker : ConnectorWorker
    {
        public TelemetryConnectorWorker(ILogger<ConnectorWorker> logger, MqttSessionClient mqttSessionClient, IDatasetSamplerFactory datasetSamplerFactory) : base(logger, mqttSessionClient, datasetSamplerFactory)
        {
        }

        /// <summary>
        /// Publish data read from the asset to the MQTT broker as telemetry
        /// </summary>
        /// <param name="mqttSessionClient">A connected MQTT client.</param>
        /// <param name="asset">The asset that this data came from.</param>
        /// <param name="dataset">The dataset that this data came from.</param>
        /// <param name="payload">The data retrieved from the asset.</param>
        /// <param name="logger">The connector worker logger.</param>
        /// <returns></returns>
        public override async Task ConnectDataAsync(MqttSessionClient mqttSessionClient, Asset asset, Dataset dataset, byte[] payload, ILogger<ConnectorWorker> logger)
        {
            logger.LogInformation($"Read dataset with name {dataset.Name} from asset with name {asset.DisplayName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(payload)}");

            var topic = dataset.Topic != null ? dataset.Topic! : asset.DefaultTopic!;
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = payload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            var puback = await mqttSessionClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
            {
                // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString}");
            }
            else
            {
                logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}
