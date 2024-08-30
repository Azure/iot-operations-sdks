using TestEnvoys.dtmi_rpc_samples_math__1;
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MathEnvoyTests
{ 
    [Fact]
    public async Task IsPrime_OneInvoker_SecondCallFromCache()
    {
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(mqttInvoker);

        await mathService.StartAsync();

        Stopwatch clock = Stopwatch.StartNew();
        var resp = await mathClient.IsPrimeAsync(executorId, new IsPrimeCommandRequest() { IsPrimeRequest = new Object_IsPrime_Request() { Number = 4567 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        var firstCall = clock.Elapsed;
        Assert.True(resp.Response.IsPrimeResponse.IsPrime);

        clock.Reset();
        resp = await mathClient.IsPrimeAsync(executorId, new IsPrimeCommandRequest() { IsPrimeRequest = new Object_IsPrime_Request() { Number = 4567 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata(); ;
        var secondCalCall = clock.Elapsed;
        Assert.True(resp.Response.IsPrimeResponse.IsPrime);

        Assert.True(firstCall > secondCalCall);
    }

    [Fact]
    public async Task IsPrime_BigNumber_Expects_Timeout()
    {
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(mqttExecutor);
        mathService.IsPrimeCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(1);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(mqttInvoker);
        
        await mathService.StartAsync();
        var ex = await Assert.ThrowsAsync<AkriMqttException>(
            () => mathClient.IsPrimeAsync(executorId, new IsPrimeCommandRequest() { IsPrimeRequest = new Object_IsPrime_Request() { Number = 45677 } },
            new RPC.CommandRequestMetadata(), TimeSpan.FromSeconds(30)).WithMetadata());

        Assert.True(ex.IsRemote);
    }

    [Fact]
    public async Task Fibonacci_OneInvoker()
    {
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(mqttInvoker);

        await mathService.StartAsync();

        var resp = await mathClient.FibAsync(executorId, new FibCommandRequest { FibRequest = new Object_Fib_Request { Number = 13 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(233, resp.Response.FibResponse.FibResult);
    }

    [Fact()]
    public async Task RandomOneInvoker()
    {
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(mqttInvoker);
        
        await mathService.StartAsync();

        var resp = await mathClient.GetRandomAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.True(resp.Response.GetRandomResponse > -1);
        Assert.True(resp.Response.GetRandomResponse < 51);
    }
}
