// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

namespace CloudEventsSample;

public class OvenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : CloudEvents.Oven.Oven.Service(applicationContext, mqttClient)
{

}
