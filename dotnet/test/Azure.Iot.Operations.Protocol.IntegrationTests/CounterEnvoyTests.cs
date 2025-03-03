// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.Counter;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterEnvoyTests
{
    [Fact]
    public async Task IncrementTest()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "counter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);

        await using CounterService counterService = new CounterService(applicationContext, mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using CounterClient counterClient = new CounterClient(applicationContext, mqttInvoker);

        await counterService.StartAsync(null, CancellationToken.None);

        IncrementRequestPayload payload = new IncrementRequestPayload();
        payload.IncrementValue = 1;

        var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp.Response.CounterResponse);

        var resp2 = await counterClient.IncrementAsync(executorId, payload, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(1, resp2.Response.CounterResponse);

        var resp3 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(1, resp3.Response.CounterResponse);

        await counterClient.IncrementAsync(executorId, payload).WithMetadata();
        await counterClient.IncrementAsync(executorId, payload).WithMetadata();
        await counterClient.IncrementAsync(executorId, payload).WithMetadata();

        var resp4 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(4, resp4.Response.CounterResponse);

        await counterClient.ResetAsync(executorId).WithMetadata();

        var resp5 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp5.Response.CounterResponse);
    }
 
    [Fact]
    public async Task DuplicateCorrelationIDThrows()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "counter-server-" + Guid.NewGuid();
        ApplicationContext appContext = new ApplicationContext();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);

        await using CounterService counterService = new CounterService(applicationContext, mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using CounterClient counterClient = new CounterClient(appContext, mqttInvoker);

        await counterService.StartAsync(null, CancellationToken.None);

        var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp.Response.CounterResponse);
        
        CommandRequestMetadata reqMd2 = new();
        IncrementRequestPayload payload = new IncrementRequestPayload();
        payload.IncrementValue = 1;
        Task[] tasks = new Task[2];
        for (int i = 0; i < tasks.Length; i++)
        {
            Task<ExtendedResponse<IncrementResponsePayload>> incrCounterTask = counterClient.IncrementAsync(executorId, payload, reqMd2).WithMetadata();
            tasks[i] = incrCounterTask;
        }
        var exception = await Assert.ThrowsAsync<AkriMqttException>(() => Task.WhenAll(tasks));
        Assert.Equal("Command 'increment' invocation failed due to duplicate request with same correlationId" ,exception.Message);
    } 

    [Fact]
    public async Task WrongCorrelationIDThrows()
    {
        string executorId = "counter-server-" + Guid.NewGuid();
        ApplicationContext applicationContext = new ApplicationContext();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using CounterService counterService = new CounterService(applicationContext, mqttExecutor);
        await counterService.StartAsync(null, CancellationToken.None);

        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync("counter-client-bad");

        TaskCompletionSource<MqttApplicationMessageReceivedEventArgs> tcs = new();
        mqttInvoker.ApplicationMessageReceivedAsync += msg =>
        {
            tcs.SetResult(msg);
            return Task.CompletedTask;
        };

        var subscribeOptions = new MqttClientSubscribeOptions($"clients/counter-client-bad/rpc/command-samples/{executorId}/readCounter");
        await mqttInvoker.SubscribeAsync(subscribeOptions);

        var pubAck = await mqttInvoker.PublishAsync(
            new MqttApplicationMessage($"rpc/command-samples/{executorId}/readCounter")
            {
                CorrelationData = [0x01, 0xAA],
                ResponseTopic = $"clients/counter-client-bad/rpc/command-samples/{executorId}/readCounter",
                MessageExpiryInterval = 10,
            });

        MqttApplicationMessageReceivedEventArgs respMsg = await tcs.Task.WaitAsync(TimeSpan.FromMinutes(1));
        var userProps = respMsg.ApplicationMessage.UserProperties;
        Assert.Equal(6, userProps!.Count); // The user props are __stat, __stMsg, __protVer, __ts, __apErr, __propName.
        Assert.Equal("400", userProps.Where( p => p.Name == "__stat").First().Value);
        Assert.Equal("Correlation data bytes do not conform to a GUID.", userProps.FirstOrDefault(p => p.Name == "__stMsg")!.Value);
        Assert.Equal("Correlation Data", userProps.Where(p => p.Name == "__propName").First().Value);
    }

}
