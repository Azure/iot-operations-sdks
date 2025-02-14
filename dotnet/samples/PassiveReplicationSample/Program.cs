// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Services.PassiveReplicationSample
{
    internal sealed class Program
    {
        private const string leadershipPositionId = "6280c032-bc0e-44d6-bda0-bce653da3e2f"; // randomly generated, but consistent across pods

        public static void Main(string[] args)
        {
            // When this code runs in a pod, this environment variable usually contains the pod's name
            string nodeId = Environment.GetEnvironmentVariable("HOSTNAME") ?? Guid.NewGuid().ToString();

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Services
                .AddSingleton(MqttClientFactoryProvider.MqttClientFactory)
                .AddTransient<StateStoreClient>(serviceProvider => new StateStoreClient(serviceProvider.GetService<MqttSessionClient>()!))
                .AddTransient<LeaderElectionClient>(serviceProvider => new LeaderElectionClient(serviceProvider.GetService<MqttSessionClient>()!, leadershipPositionId, nodeId))
                .AddHostedService<PassiveReplicationNode>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}