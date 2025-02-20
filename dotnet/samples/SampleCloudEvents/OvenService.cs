// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

namespace SampleCloudEvents;

public class OvenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : dtmi_akri_samples_oven__1.Oven.Service(applicationContext, mqttClient)
{

}
