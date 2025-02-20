// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TestEnvoys.Memmon;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class MemmonClient : Memmon.Client
{
    public TaskCompletionSource WorkingSetTelemetryReceivedTcs = new();

    public List<WorkingSetTelemetry> ReceivedWorkingSetTelemetry { get; set; } = new();
    
    public List<IncomingTelemetryMetadata> ReceivedWorkingSetTelemetryMetadata { get; set; } = new();

    public TaskCompletionSource ManagedMemoryTelemetryReceivedTcs = new();

    public List<ManagedMemoryTelemetry> ReceivedManagedMemoryTelemetry { get; set; } = new();

    public List<IncomingTelemetryMetadata> ReceivedManagedMemoryTelemetryMetadata { get; set; } = new();

    public TaskCompletionSource MemoryStatsTelemetryReceivedTcs = new();

    public List<MemoryStatsTelemetry> ReceivedMemoryStatsTelemetry { get; set; } = new();

    public List<IncomingTelemetryMetadata> ReceivedMemoryStatsTelemetryMetadata { get; set; } = new();

    public MemmonClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : base(applicationContext, mqttClient)
    {

    }

    public override Task ReceiveTelemetry(string senderId, WorkingSetTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        ReceivedWorkingSetTelemetry.Add(telemetry);
        ReceivedWorkingSetTelemetryMetadata.Add(metadata);
        WorkingSetTelemetryReceivedTcs.TrySetResult();
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, ManagedMemoryTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        ReceivedManagedMemoryTelemetry.Add(telemetry);
        ReceivedManagedMemoryTelemetryMetadata.Add(metadata);
        ManagedMemoryTelemetryReceivedTcs.TrySetResult();
        return Task.CompletedTask;
    }

    public override Task ReceiveTelemetry(string senderId, MemoryStatsTelemetry telemetry, IncomingTelemetryMetadata metadata)
    {
        ReceivedMemoryStatsTelemetry.Add(telemetry);
        ReceivedMemoryStatsTelemetryMetadata.Add(metadata);
        MemoryStatsTelemetryReceivedTcs.TrySetResult();
        return Task.CompletedTask;
    }
}

public class MemMonEnvoyTests
{
    [Fact]
    public async Task Send_ReceiveTelemetry()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemmonClient memmonClient = new(applicationContext, mqttReceiver);
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemMonService memMonService = new(applicationContext, mqttSender);

        await memmonClient.StartAsync();

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetry);

        await memMonService.SendTelemetryAsync(new MemoryStatsTelemetry() { MemoryStats = new MemoryStatsSchema { ManagedMemory = 3, WorkingSet = 4 } }, new OutgoingTelemetryMetadata());
        await memMonService.SendTelemetryAsync(new WorkingSetTelemetry() { WorkingSet = 1 }, new OutgoingTelemetryMetadata());
        await memMonService.SendTelemetryAsync(new ManagedMemoryTelemetry() { ManagedMemory = 2 }, new OutgoingTelemetryMetadata());

        // Wait for all receivers to receive some telemetry, or time out after a while.
        await Task.WhenAll(
            memmonClient.WorkingSetTelemetryReceivedTcs.Task,
            memmonClient.MemoryStatsTelemetryReceivedTcs.Task,
            memmonClient.ManagedMemoryTelemetryReceivedTcs.Task).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Single(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Single(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Single(memmonClient.ReceivedWorkingSetTelemetry);
    }

   

    [Fact]
    public async Task Send_ReceiveTelemetryWithMetadataAndCE()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemmonClient memmonClient = new(applicationContext, mqttReceiver);
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemMonService memMonService = new(applicationContext, mqttSender);

        await memmonClient.StartAsync();

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetry);

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetryMetadata);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetryMetadata);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetryMetadata);

        var MemoryStatsCorrelationId = Guid.NewGuid();
        var MemoryStatsUserDataKey = Guid.NewGuid().ToString();
        var MemoryStatsUserDataValue = Guid.NewGuid().ToString();
        var MemoryStatsTelemetryMetadata = new OutgoingTelemetryMetadata() { CloudEvent = new CloudEvent(new Uri("test://mq")) };
        MemoryStatsTelemetryMetadata.UserData.Add(MemoryStatsUserDataKey, MemoryStatsUserDataValue);
        await memMonService.SendTelemetryAsync(new MemoryStatsTelemetry() { MemoryStats = new MemoryStatsSchema { ManagedMemory = 3, WorkingSet = 4 } }, MemoryStatsTelemetryMetadata);

        var WorkingSetCorrelationId = Guid.NewGuid();
        var WorkingSetUserDataKey = Guid.NewGuid().ToString();
        var WorkingSetUserDataValue = Guid.NewGuid().ToString();
        var WorkingSetTelemetryMetadata = new OutgoingTelemetryMetadata();
        WorkingSetTelemetryMetadata.UserData.Add(WorkingSetUserDataKey, WorkingSetUserDataValue);
        await memMonService.SendTelemetryAsync(new WorkingSetTelemetry() { WorkingSet = 1 }, WorkingSetTelemetryMetadata);

        var ManagedMemoryCorrelationId = Guid.NewGuid();
        var ManagedMemoryUserDataKey = Guid.NewGuid().ToString();
        var ManagedMemoryUserDataValue = Guid.NewGuid().ToString();
        var ManagedMemoryTelemetryMetadata = new OutgoingTelemetryMetadata() { CloudEvent = new CloudEvent(new Uri("test://mq")) };
        ManagedMemoryTelemetryMetadata.UserData.Add(ManagedMemoryUserDataKey, ManagedMemoryUserDataValue);
        await memMonService.SendTelemetryAsync(new ManagedMemoryTelemetry() { ManagedMemory = 2 }, ManagedMemoryTelemetryMetadata);

        Console.WriteLine("Application context before sending telemetry");
        Console.WriteLine(applicationContext.ApplicationHlc);

        // Wait for all receivers to receive some telemetry, or time out after a while.
        await Task.WhenAll(
            memmonClient.WorkingSetTelemetryReceivedTcs.Task,
            memmonClient.MemoryStatsTelemetryReceivedTcs.Task,
            memmonClient.ManagedMemoryTelemetryReceivedTcs.Task).WaitAsync(TimeSpan.FromSeconds(30));

        Console.WriteLine(MemoryStatsTelemetryMetadata.Timestamp);
        Console.WriteLine(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp);
        Console.WriteLine($"MemoryStatsTelemetryMetadata.Timestamp: {MemoryStatsTelemetryMetadata.Timestamp}");
        Console.WriteLine($"memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp: {memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp}");
        Console.WriteLine($"WorkingSetTelemetryMetadata.Timestamp: {WorkingSetTelemetryMetadata.Timestamp}");
        Console.WriteLine($"memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp: {memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp}");
        Console.WriteLine($"ManagedMemoryTelemetryMetadata.Timestamp: {ManagedMemoryTelemetryMetadata.Timestamp}");
        Console.WriteLine($"memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp: {memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp}");
        Console.WriteLine("Application context after completion of telemetry");
        Console.WriteLine(applicationContext.ApplicationHlc);

        Assert.Single(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Single(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Single(memmonClient.ReceivedWorkingSetTelemetry);

        Assert.Single(memmonClient.ReceivedManagedMemoryTelemetryMetadata);
        Assert.Single(memmonClient.ReceivedMemoryStatsTelemetryMetadata);
        Assert.Single(memmonClient.ReceivedWorkingSetTelemetryMetadata);
        Assert.Single(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData);

        var memStatsMD = memmonClient.ReceivedMemoryStatsTelemetryMetadata[0];
        Assert.NotNull(memStatsMD);
        Assert.NotNull(memStatsMD.UserData);
        Assert.Equal(8, memStatsMD.UserData.Count);
        Assert.NotNull(memStatsMD.GetCloudEvent());
        Assert.Equal("1.0", memStatsMD.GetCloudEvent().SpecVersion);
        Assert.Equal("test://mq/", memStatsMD.GetCloudEvent().Source!.ToString());
        Assert.Equal("ms.aio.telemetry", memStatsMD.GetCloudEvent().Type);
        Assert.Equal($"rpc/samples/dtmi:akri:samples:memmon;1/{mqttSender.ClientId}/memoryStats", memStatsMD.GetCloudEvent().Subject);
        //Assert.Equal("1.0", memStatsMD.GetCloudEvent().DataSchema);
        Assert.Equal("application/avro", memStatsMD.GetCloudEvent().DataContentType);
        Assert.True(DateTime.TryParse(memStatsMD.GetCloudEvent().Time!.Value.ToString("o"), out DateTime _));
        Assert.True(Guid.TryParse(memStatsMD.GetCloudEvent().Id, out Guid _));
        Assert.Equal(mqttSender.ClientId, memStatsMD.SenderId);

        var ManagedMemoryMD = memmonClient.ReceivedManagedMemoryTelemetryMetadata[0];
        Assert.NotNull(ManagedMemoryMD);
        Assert.NotNull(ManagedMemoryMD.UserData);
        Assert.Equal(8, ManagedMemoryMD.UserData.Count);
        Assert.Equal("1.0", ManagedMemoryMD.GetCloudEvent()!.SpecVersion);
        Assert.Equal("test://mq/", ManagedMemoryMD.GetCloudEvent().Source!.ToString());
        Assert.Equal("ms.aio.telemetry", ManagedMemoryMD.GetCloudEvent().Type);
        Assert.Equal($"rpc/samples/dtmi:akri:samples:memmon;1/{mqttSender.ClientId}/managedMemory", ManagedMemoryMD.GetCloudEvent().Subject);
        //Assert.Equal("1.0", ManagedMemoryMD.GetCloudEvent().DataSchema);
        Assert.Equal("application/avro", ManagedMemoryMD.GetCloudEvent().DataContentType);
        Assert.True(DateTime.TryParse(ManagedMemoryMD.GetCloudEvent().Time!.Value.ToString("o"), out DateTime _));
        Assert.True(Guid.TryParse(ManagedMemoryMD.GetCloudEvent().Id, out Guid _));
        Assert.Equal(mqttSender.ClientId, ManagedMemoryMD.SenderId);

        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData.ContainsKey(MemoryStatsUserDataKey));
        Assert.Equal(MemoryStatsUserDataValue, memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData[MemoryStatsUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, MemoryStatsTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp!));

        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData);
        Assert.False(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData.ContainsKey("dataschema"));
        //Assert.Equal("TODO", memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData["dataschema"]);

        Assert.NotNull(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData.ContainsKey(WorkingSetUserDataKey));
        Assert.Equal(WorkingSetUserDataValue, memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData[WorkingSetUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, WorkingSetTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp!));

        Assert.NotNull(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData.ContainsKey(ManagedMemoryUserDataKey));
        Assert.Equal(ManagedMemoryUserDataValue, memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData[ManagedMemoryUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, ManagedMemoryTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp!));
    }

    [Fact]
    public async Task Commands()
    {
        ApplicationContext applicationContext = new ApplicationContext();
        string invokerId = "test-invoker-" + Guid.NewGuid();
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync(invokerId);
        await using MemmonClient memmonClient = new(applicationContext, mqttReceiver);

        string executorId = "test-executor-" + Guid.NewGuid();
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MemMonService memMonService = new(applicationContext, mqttSender);
        await memmonClient.StartAsync();
        await memMonService.StartAsync();

        var resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { DiagnosticsMode = GetRuntimeStatsRequestSchema.full }, commandTimeout: TimeSpan.FromSeconds(30));

        Assert.NotNull(resp);

        var startResp = await memmonClient.StartTelemetryAsync(executorId, new StartTelemetryRequestPayload() { Interval = 4 });

        resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { DiagnosticsMode = GetRuntimeStatsRequestSchema.full }, commandTimeout: TimeSpan.FromSeconds(30));
        Assert.Equal("4", resp.DiagnosticResults["interval"]);
        Assert.Equal("True", resp.DiagnosticResults["enabled"]);

        await memmonClient.StopTelemetryAsync(executorId);
        resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { DiagnosticsMode = GetRuntimeStatsRequestSchema.full }, commandTimeout: TimeSpan.FromSeconds(30));
        Assert.Equal("False", resp.DiagnosticResults["enabled"]);
    }

    void AssertUserProperty(Dictionary<string, string> props, string name, string value)
    {

        var prop = props.FirstOrDefault(x => x.Key == name); 

        if (props is not null)
        {
            Assert.Equal(value, prop!.Value);
        }
        else
        {
            Assert.Fail($"{name} not found in UserProperties");
        }
    }
}
