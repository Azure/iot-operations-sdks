// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

namespace SampleCloudEvents;

public class OvenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : SampleCloudEvents.Oven.Oven.Service(applicationContext, mqttClient)
{

}
