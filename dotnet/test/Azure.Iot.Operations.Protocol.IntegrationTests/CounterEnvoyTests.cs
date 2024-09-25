using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterEnvoyTests
{
    [Fact]
    public async Task IncrementTest()
    {
        string executorId = "counter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);

        await using CounterService counterService = new CounterService(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using CounterClient counterClient = new CounterClient(mqttInvoker);

        await counterService.StartAsync(null, CancellationToken.None);

        var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp.Response.CounterResponse);

        var resp2 = await counterClient.IncrementAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(1, resp2.Response.CounterResponse);

        var resp3 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(1, resp3.Response.CounterResponse);

        await counterClient.IncrementAsync(executorId).WithMetadata();
        await counterClient.IncrementAsync(executorId).WithMetadata();
        await counterClient.IncrementAsync(executorId).WithMetadata();

        var resp4 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(4, resp4.Response.CounterResponse);

        await counterClient.ResetAsync(executorId).WithMetadata();

        var resp5 = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp5.Response.CounterResponse);
    }
 
    [Fact]
    public async Task DuplicateCorrelationIDThrows()
    {
        string executorId = "counter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);

        await using CounterService counterService = new CounterService(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using CounterClient counterClient = new CounterClient(mqttInvoker);

        await counterService.StartAsync(null, CancellationToken.None);

        var resp = await counterClient.ReadCounterAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(0, resp.Response.CounterResponse);
        
        CommandRequestMetadata reqMd2 = new();
        Task[] tasks = new Task[2];
        for (int i = 0; i < tasks.Length; i++)
        {
            Task<ExtendedResponse<IncrementCommandResponse>> incrCounterTask = counterClient.IncrementAsync(executorId, reqMd2).WithMetadata();
            tasks[i] = incrCounterTask;
        }
        var exception = await Assert.ThrowsAsync<AkriMqttException>(() => Task.WhenAll(tasks));
        Assert.Equal("Command 'increment' invocation failed due to duplicate request with same correlationId" ,exception.Message);
    } 

    

    [Fact]
    public async Task WrongCorrelationIDThrows()
    {
        string executorId = "counter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using CounterService counterService = new CounterService(mqttExecutor);
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
        Assert.Equal(5, userProps!.Count);
        Assert.Equal("400", userProps.Where( p => p.Name == "__stat").First().Value);
        Assert.Equal("Correlation data bytes do not conform to a GUID.", userProps.FirstOrDefault(p => p.Name == "__stMsg")!.Value);
        Assert.Equal("Correlation Data", userProps.Where(p => p.Name == "__propName").First().Value);
    }

}
