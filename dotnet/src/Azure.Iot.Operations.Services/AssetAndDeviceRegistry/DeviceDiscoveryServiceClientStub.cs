// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class DeviceDiscoveryServiceClientStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : Generated.DeviceDiscoveryService.DeviceDiscoveryService.Client(applicationContext, mqttClient, topicTokenMap), IDeviceDiscoveryServiceClientStub;
