// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    // This is the class that makes the abstract generated code concrete
    internal class StateStoreGeneratedClient : StateStore.StateStore.Client
    {
        internal StateStoreGeneratedClient(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient) : base(applicationContext, mqttPubSubClient)
        {
        }
    }
}
