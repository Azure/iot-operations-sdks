// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TestEnvoys.Math;
using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MathEnvoyTests
{ 
    [Fact]
    public async Task IsPrime_OneInvoker_SecondCallFromCache()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(applicationContext, mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(applicationContext, mqttInvoker);

        await mathService.StartAsync();

        Stopwatch clock = Stopwatch.StartNew();
        var resp = await mathClient.IsPrimeAsync(executorId, new IsPrimeRequestPayload() { IsPrimeRequest = new IsPrimeRequestSchema() { Number = 4567 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        var firstCall = clock.Elapsed;
        Assert.True(resp.Response.IsPrimeResponse.IsPrime);

        clock.Reset();
        resp = await mathClient.IsPrimeAsync(executorId, new IsPrimeRequestPayload() { IsPrimeRequest = new IsPrimeRequestSchema() { Number = 4567 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata(); ;
        var secondCalCall = clock.Elapsed;
        Assert.True(resp.Response.IsPrimeResponse.IsPrime);

        Assert.True(firstCall > secondCalCall);
    }

    [Fact]
    public async Task IsPrime_BigNumber_Expects_Timeout()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(applicationContext, mqttExecutor);
        mathService.IsPrimeCommandExecutor.ExecutionTimeout = TimeSpan.FromSeconds(1);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(applicationContext, mqttInvoker);
        
        await mathService.StartAsync();
        var ex = await Assert.ThrowsAsync<AkriMqttException>(
            () => mathClient.IsPrimeAsync(executorId, new IsPrimeRequestPayload() { IsPrimeRequest = new IsPrimeRequestSchema() { Number = 45677 } },
            new RPC.CommandRequestMetadata(), TimeSpan.FromSeconds(30)).WithMetadata());

        Assert.True(ex.IsRemote);
    }

    [Fact]
    public async Task Fibonacci_OneInvoker()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(applicationContext, mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(applicationContext, mqttInvoker);

        await mathService.StartAsync();

        var resp = await mathClient.FibAsync(executorId, new FibRequestPayload { FibRequest = new FibRequestSchema { Number = 13 } }, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.Equal(233, resp.Response.FibResponse.FibResult);
    }

    [Fact()]
    public async Task RandomOneInvoker()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string executorId = "math-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MathService mathService = new(applicationContext, mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MathClient mathClient = new(applicationContext, mqttInvoker);
        
        await mathService.StartAsync();

        var resp = await mathClient.GetRandomAsync(executorId, commandTimeout: TimeSpan.FromSeconds(30)).WithMetadata();
        Assert.True(resp.Response.GetRandomResponse > -1);
        Assert.True(resp.Response.GetRandomResponse < 51);
    }
}
