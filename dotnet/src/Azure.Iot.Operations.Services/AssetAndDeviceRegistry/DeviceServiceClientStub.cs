// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class DeviceServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AepTypeService.AepTypeService.Client(applicationContext, mqttClient, topicTokenMap);
