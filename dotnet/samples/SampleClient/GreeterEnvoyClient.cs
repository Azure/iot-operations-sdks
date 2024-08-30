using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Greeter;

namespace SampleClient;

internal class GreeterEnvoyClient(MqttSessionClient mqttClient) : GreeterEnvoy.Client(mqttClient)
{
}
