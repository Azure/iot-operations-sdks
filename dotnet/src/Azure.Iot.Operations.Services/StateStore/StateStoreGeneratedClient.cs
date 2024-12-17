using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Services.StateStore
{
    // This is the class that makes the abstract generated code concrete
    internal class StateStoreGeneratedClient : dtmi_ms_aio_mq_StateStore__1.StateStore.Client
    {
        internal StateStoreGeneratedClient(IMqttPubSubClient mqttPubSubClient) : base (mqttPubSubClient)
        { 
        }
    }
}
