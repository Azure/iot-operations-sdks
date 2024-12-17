// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using System.Diagnostics;
using TestEnvoys.Greeter;
using Azure.Iot.Operations.Mqtt;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class GreeterEnvoyTests
{
    [Fact]
    public async Task SayHello()
    {
        string executorId = "greeter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using GreeterService greeterService = new GreeterService(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using GreeterEnvoy.Client greeterClient = new GreeterEnvoy.Client(mqttInvoker);

        await greeterService.StartAsync();

        var resp = await greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "Rido"
            }
        }, timeout: TimeSpan.FromSeconds(30)).WithMetadata();

        Assert.Equal("Hello Rido", resp.Response.Message);
    }

    [Fact]
    public async Task SayHelloWithDelay_FromCache()
    {
        string executorId = "greeter-server-" + Guid.NewGuid();
        await using MqttSessionClient mqttExecutor = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using GreeterService greeterService = new GreeterService(mqttExecutor);
        await using MqttSessionClient mqttInvoker = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using GreeterEnvoy.Client greeterClient = new GreeterEnvoy.Client(mqttInvoker);

        await greeterService.StartAsync();

        Stopwatch clock = Stopwatch.StartNew();
        var resp = await greeterClient.SayHelloWithDelay(new ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest>
        {
            Request = new GreeterEnvoy.HelloWithDelayRequest
            {
                Name = "Rido",
                Delay = TimeSpan.FromSeconds(4)
            }
        }, TimeSpan.FromSeconds(30)).WithMetadata();
        var firstCallTime = clock.Elapsed;

        clock.Restart();
        resp = await greeterClient.SayHelloWithDelay(new ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest>
        {
            Request = new GreeterEnvoy.HelloWithDelayRequest
            {
                Name = "Rido",
                Delay = TimeSpan.FromSeconds(4)
            }
        }, TimeSpan.FromSeconds(30)).WithMetadata();
        var secondCallTime = clock.Elapsed;

        Assert.Equal("Hello Rido after 00:00:04", resp.Response.Message);

        Assert.True(firstCallTime > secondCallTime);
    }

    [Fact]
    public async Task SayHelloWithDelay_ExecutorTimeout()
    {
        await using MqttSessionClient mqttExecutorClient = await ClientFactory.CreateSessionClientFromEnvAsync($"executor_{Guid.NewGuid()}");
        await using MqttSessionClient mqttInvokerClient = await ClientFactory.CreateSessionClientFromEnvAsync($"invoker_{Guid.NewGuid()}");
        await using GreeterService greeterService = new(mqttExecutorClient);
        await using GreeterEnvoy.Client greeterClient = new(mqttInvokerClient);

        greeterService.SetExecutorTimeout(TimeSpan.FromSeconds(5));
        await greeterService.StartAsync();

        RpcCallAsync<GreeterEnvoy.HelloResponse> greeterResponseCall = greeterClient.SayHelloWithDelay(new ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest>
        {
            Request = new GreeterEnvoy.HelloWithDelayRequest
            {
                Name = nameof(SayHelloWithDelay_ExecutorTimeout),
                Delay = TimeSpan.FromSeconds(10)
            }
        });

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await greeterResponseCall);
        Assert.Equal(AkriMqttErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task SayHelloWithDelayZeroThrows()
    {
        await using OrderedAckMqttClient mqttExecutorClient = await ClientFactory.CreateClientAsyncFromEnvAsync($"executor_{Guid.NewGuid()}");
        await using OrderedAckMqttClient mqttInvokerClient = await ClientFactory.CreateClientAsyncFromEnvAsync($"invoker_{Guid.NewGuid()}");

        await using GreeterService greeterService = new(mqttExecutorClient);
        await using GreeterEnvoy.Client greeterClient = new(mqttInvokerClient);

        await greeterService.StartAsync();

        RpcCallAsync<GreeterEnvoy.HelloResponse> greeterResponseCall = greeterClient.SayHelloWithDelay(new ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest>
        {
            Request = new GreeterEnvoy.HelloWithDelayRequest
            {
                Name = nameof(SayHelloWithDelay_ExecutorTimeout),
                Delay = TimeSpan.Zero
            }
        });
        await Task.Delay(100);
        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await greeterResponseCall);
        Assert.Equal(AkriMqttErrorKind.ExecutionException, ex.Kind);
        Assert.True(ex.IsRemote);
        Assert.Equal("Delay cannot be Zero", ex.Message);
    }

    [Fact(Skip = "This test requires the session client which hasn't been finished yet")]
    public async Task UnacknowledgedCommandExpiresOnReconnect()
    {
        await using MqttSessionClient mqttExecutorClient = await ClientFactory.CreateSessionClientFromEnvAsync("executor9-" + Guid.NewGuid());
        await using MqttSessionClient mqttInvokerClient = await ClientFactory.CreateSessionClientFromEnvAsync("invoker9-" + Guid.NewGuid());

        await using GreeterService greeterService = new GreeterService(mqttExecutorClient);
        await using GreeterEnvoy.Client greeterClient = new GreeterEnvoy.Client(mqttInvokerClient);

        await greeterService.StartAsync();

        RpcCallAsync<GreeterEnvoy.HelloResponse> longRunningTask = greeterClient.SayHelloWithDelay(new ExtendedRequest<GreeterEnvoy.HelloWithDelayRequest>
        {
            Request = new GreeterEnvoy.HelloWithDelayRequest
            {
                Name = "User1",
                Delay = TimeSpan.FromSeconds(18)
            }
        },
        timeout: TimeSpan.FromSeconds(30));

        await Task.Delay(TimeSpan.FromSeconds(1));  // Delay to ensure longRunningTask is enqueued before quickFinishingTask

        RpcCallAsync<GreeterEnvoy.HelloResponse> quickFinishingTask = greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "User2",
            }
        },
        timeout: TimeSpan.FromSeconds(3));

        string helloResult = (await quickFinishingTask).Message;
        Assert.Equal("Hello User2", helloResult);

        await Task.Delay(TimeSpan.FromSeconds(5));  // Delay to ensure quickFinishingTask expires before reconnect

        await mqttExecutorClient.DisconnectAsync();
        await mqttExecutorClient.ReconnectAsync();

        string helloWithDelayResult = (await longRunningTask).Message;
        Assert.Equal("Hello User1 after 00:00:18", helloWithDelayResult);

        await Task.Delay(TimeSpan.FromSeconds(5));  // Delay to allow for manual delayed acks to complete

        GreeterEnvoy.HelloResponse hello2Result = await greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "User3",
            }
        }, timeout: TimeSpan.FromSeconds(30));
        Assert.Equal("Hello User3", hello2Result.Message);

        await Task.Delay(TimeSpan.FromSeconds(5));  // Delay to allow for manual delayed acks to complete
    }

    [Fact]
    public async Task TestSharedSubscriptionWithTwoExecutors()
    {
        /// connects 2 command executors on one shared subscription
        /// asserts that command invoked on shared topic is handled by one and only one executor
        string executorId1 = "executor-1-" + Guid.NewGuid();
        string executorId2 = "executor-2-" + Guid.NewGuid();

        // create executors
        await using MqttSessionClient mqttExecutor1 = await ClientFactory.CreateSessionClientFromEnvAsync(executorId1);
        await using GreeterService greeterService1 = new GreeterService(mqttExecutor1);

        await using MqttSessionClient mqttExecutor2 = await ClientFactory.CreateSessionClientFromEnvAsync(executorId2);
        await using GreeterService greeterService2 = new GreeterService(mqttExecutor2);

        // add count for each executor's commands handled
        int count1 = 0;
        greeterService1.SayHelloCommandExecutor.OnCommandReceived = (req, ct) =>
        {
            count1++;
            return Task.FromResult(new ExtendedResponse<GreeterEnvoy.HelloResponse>
            {
                Response = new GreeterEnvoy.HelloResponse
                {
                    Message = "Hello " + req.Request.Name
                }
            });
        };

        int count2 = 0;
        greeterService2.SayHelloCommandExecutor.OnCommandReceived = (req, ct) =>
        {
            count2++;
            return Task.FromResult(new ExtendedResponse<GreeterEnvoy.HelloResponse>
            {
                Response = new GreeterEnvoy.HelloResponse
                {
                    Message = "Hello " + req.Request.Name
                }
            });
        };

        // create invoker
        await using MqttSessionClient mqttInvokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using GreeterEnvoy.Client greeterClient = new GreeterEnvoy.Client(mqttInvokerClient);

        // start services
        await greeterService1.StartAsync();
        await greeterService2.StartAsync();

        // invoke on sharedTopic
        // invokers and executors are hardcoded to use topic "rpc/samples/hello"
        var resp = await greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "Sharing executor"
            }
        }, timeout: TimeSpan.FromSeconds(30)).WithMetadata();

        // verify that command was handled
        Assert.Equal("Hello Sharing executor", resp.Response.Message);

        // verify only one executor handled the command
        Assert.Equal(1, count1 + count2);
    }

    [Fact(Skip = "Waiting for $partition to be ready")]
    public async Task TestMultipleExecutorsWithPartitionId()
    {
        /// connects 2 command executors on one shared subscription
        /// asserts that command invoked on shared topic is handled by one and only one executor
        string executorId1 = "executor-1-" + Guid.NewGuid();
        string executorId2 = "executor-2-" + Guid.NewGuid();

        // create executors
        await using MqttSessionClient mqttExecutor1 = await ClientFactory.CreateSessionClientFromEnvAsync(executorId1);
        await using GreeterService greeterService1 = new GreeterService(mqttExecutor1);

        await using MqttSessionClient mqttExecutor2 = await ClientFactory.CreateSessionClientFromEnvAsync(executorId2);
        await using GreeterService greeterService2 = new GreeterService(mqttExecutor2);

        // add count for each executor's commands handled
        int count1 = 0;
        greeterService1.SayHelloCommandExecutor.OnCommandReceived = (req, ct) =>
        {
            count1++;
            return Task.FromResult(new ExtendedResponse<GreeterEnvoy.HelloResponse>
            {
                Response = new GreeterEnvoy.HelloResponse
                {
                    Message = "Hello " + req.Request.Name
                }
            });
        };

        int count2 = 0;
        greeterService2.SayHelloCommandExecutor.OnCommandReceived = (req, ct) =>
        {
            count2++;
            return Task.FromResult(new ExtendedResponse<GreeterEnvoy.HelloResponse>
            {
                Response = new GreeterEnvoy.HelloResponse
                {
                    Message = "Hello " + req.Request.Name
                }
            });
        };

        // create invoker
        await using MqttSessionClient mqttInvokerClient = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using GreeterEnvoy.Client greeterClient = new GreeterEnvoy.Client(mqttInvokerClient);

        // start greeter services
        await greeterService1.StartAsync();
        await greeterService2.StartAsync();

        // invoke to singular executor
        var resp1 = await greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "Invoker 1"
            },
        }, timeout: TimeSpan.FromSeconds(30)).WithMetadata();

        var resp2 = await greeterClient.SayHello(new ExtendedRequest<GreeterEnvoy.HelloRequest>
        {
            Request = new GreeterEnvoy.HelloRequest
            {
                Name = "Invoker 2"
            },
        }, timeout: TimeSpan.FromSeconds(30)).WithMetadata();

        // check received
        Assert.Equal("Hello Invoker 1", resp1.Response.Message);
        Assert.Equal("Hello Invoker 2", resp2.Response.Message);

        // verify that both invocations were handled by the same executor
        Assert.Equal(2, count1 + count2);
        Assert.True(count1 == 0 || count2 == 0);
    }
}
