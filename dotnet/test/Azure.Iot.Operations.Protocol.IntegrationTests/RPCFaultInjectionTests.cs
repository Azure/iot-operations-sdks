// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Counter;

namespace Azure.Iot.Operations.Protocol.IntegrationTests
{
    [Trait("Category", "FaultInjection")]
    public class RPCFaultInjectionTests
    {
        [Fact]
        public async Task TestRPCHandlesServiceConnectionDrop()
        {
            string executorId = "counter-server-" + Guid.NewGuid();
            await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv(null, executorId);
            await using CounterService counterService = new CounterService(mqttExecutor);
            TaskCompletionSource faultWasInjectedTcs = new();
            mqttExecutor.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs.TrySetResult();
                return Task.CompletedTask;
            };
            
            await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();
            await using CounterClient counterClient = new CounterClient(mqttInvoker);
            await counterService.StartAsync(null, CancellationToken.None);

            var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
            Assert.Equal(0, resp.Response.CounterResponse);
            
            byte[] expectedPayload = Guid.NewGuid().ToByteArray();

            // This fault injection publish will be ack'd as normal, but will tell the broker
            // to kill the connection 1 second after receiving the publish
            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.ServerBusy;
            MqttApplicationMessage faultMessage = new MqttApplicationMessage(Guid.NewGuid().ToString())
            {
                PayloadSegment = expectedPayload,
            };

            faultMessage.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            faultMessage.AddUserProperty(FaultInjectionTestConstants.disconnectFaultDelayName, "1");
            faultMessage.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());
            
            var result = await mqttExecutor.PublishAsync(faultMessage).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.True(result.IsSuccess);

            IncrementRequestPayload payload = new IncrementRequestPayload();
            payload.IncrementValue = 1;

            // // Wait until the fault injection happens or until a timeout
            await faultWasInjectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var resp2 = await counterClient.IncrementAsync(executorId, payload, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
            Assert.Equal(1, resp2.Response.CounterResponse);   
        }
        
        [Fact]
        public async Task TestRPCHandlesServiceAndClientConnectionDrop()
        {
            string executorId = "counter-server-" + Guid.NewGuid();
            await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv(null, executorId);
            await using CounterService counterService = new CounterService(mqttExecutor);
            TaskCompletionSource faultWasInjectedTcs1 = new();
            mqttExecutor.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs1.TrySetResult();
                return Task.CompletedTask;
            };
            
            await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientForFaultableBrokerFromEnv();
            await using CounterClient counterClient = new CounterClient(mqttInvoker);
            TaskCompletionSource faultWasInjectedTcs2 = new();
            mqttInvoker.DisconnectedAsync += (args) =>
            {
                faultWasInjectedTcs2.TrySetResult();
                return Task.CompletedTask;
            };
            
            await counterService.StartAsync(null, CancellationToken.None);
            var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
            Assert.Equal(0, resp.Response.CounterResponse);
            
            byte[] expectedPayload = Guid.NewGuid().ToByteArray();

            // This fault injection publish will be ack'd as normal, but will tell the broker
            // to kill the connection 1 second after receiving the publish
            MqttClientDisconnectReason expectedReason = MqttClientDisconnectReason.ServerBusy;
            MqttApplicationMessage faultMessage1 = new MqttApplicationMessage(Guid.NewGuid().ToString())
            {
                PayloadSegment = expectedPayload,
            };

            faultMessage1.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            faultMessage1.AddUserProperty(FaultInjectionTestConstants.disconnectFaultDelayName, "1");
            faultMessage1.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());
            
            var result = await mqttExecutor.PublishAsync(faultMessage1).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.True(result.IsSuccess);
            await faultWasInjectedTcs1.Task.WaitAsync(TimeSpan.FromSeconds(30));

            MqttApplicationMessage faultMessage2 = new MqttApplicationMessage(Guid.NewGuid().ToString())
            {
                PayloadSegment = expectedPayload,
            };

            faultMessage2.AddUserProperty(FaultInjectionTestConstants.disconnectFaultName, "" + ((int)expectedReason));
            faultMessage2.AddUserProperty(FaultInjectionTestConstants.disconnectFaultDelayName, "1");
            faultMessage2.AddUserProperty(FaultInjectionTestConstants.faultRequestIdName, Guid.NewGuid().ToString());

            result = await mqttInvoker.PublishAsync(faultMessage2).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.True(result.IsSuccess);
            await faultWasInjectedTcs2.Task.WaitAsync(TimeSpan.FromSeconds(30));

            IncrementRequestPayload payload = new IncrementRequestPayload();
            payload.IncrementValue = 1;

            var resp2 = await counterClient.IncrementAsync(executorId, payload, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
            Assert.Equal(1, resp2.Response.CounterResponse);   
        }
    }
}