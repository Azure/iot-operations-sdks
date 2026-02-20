// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    internal class StateStoreClientStub : Generated.StateStore.Client, IStateStoreClientStub
    {
        public StateStoreClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null) : base(applicationContext, mqttClient, topicTokenMap)
        {
        }
    }
}
