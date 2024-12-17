// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;

namespace SampleCloudEvents;

public class OvenService(MqttSessionClient mqttClient) : dtmi_akri_samples_oven__1.Oven.Service(mqttClient)
{

}
