// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;

namespace SampleReadCloudEvents;

internal class SchemaRegistryClientFactoryProvider
{
    public static Func<IServiceProvider, SchemaRegistryClient> SchemaRegistryFactory = service => new SchemaRegistryClient(service.GetRequiredService<ApplicationContext>(), service.GetService<MqttSessionClient>()!);
}
