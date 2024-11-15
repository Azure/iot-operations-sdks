using TestEnvoys.dtmi_akri_samples_memmon__1;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Mqtt.Session;
using MQTTnet.Packets;

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

    public MemmonClient(IMqttPubSubClient mqttClient) : base(mqttClient)
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
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemmonClient memmonClient = new(mqttReceiver);
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemMonService memMonService = new(mqttSender);

        await memmonClient.StartAsync();

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetry);

        await memMonService.SendTelemetryAsync(new MemoryStatsTelemetry() { memoryStats = new Object_MemoryStats { managedMemory = 3, workingSet = 4 } }, new OutgoingTelemetryMetadata());
        await memMonService.SendTelemetryAsync(new WorkingSetTelemetry() { workingSet = 1 }, new OutgoingTelemetryMetadata());
        await memMonService.SendTelemetryAsync(new ManagedMemoryTelemetry() { managedMemory = 2 }, new OutgoingTelemetryMetadata());

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
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemmonClient memmonClient = new(mqttReceiver);
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync();
        await using MemMonService memMonService = new(mqttSender);

        await memmonClient.StartAsync();

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetry);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetry);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetry);

        Assert.Empty(memmonClient.ReceivedManagedMemoryTelemetryMetadata);
        Assert.Empty(memmonClient.ReceivedMemoryStatsTelemetryMetadata);
        Assert.Empty(memmonClient.ReceivedWorkingSetTelemetryMetadata);

        var memoryStatsCorrelationId = Guid.NewGuid();
        var memoryStatsUserDataKey = Guid.NewGuid().ToString();
        var memoryStatsUserDataValue = Guid.NewGuid().ToString();
        var memoryStatsTelemetryMetadata = new OutgoingTelemetryMetadata() { CloudEvent = new CloudEvent(new Uri("test://mq")) };
        memoryStatsTelemetryMetadata.UserData.Add(memoryStatsUserDataKey, memoryStatsUserDataValue);
        await memMonService.SendTelemetryAsync(new MemoryStatsTelemetry() { memoryStats = new Object_MemoryStats { managedMemory = 3, workingSet = 4 } }, memoryStatsTelemetryMetadata);

        var workingSetCorrelationId = Guid.NewGuid();
        var workingSetUserDataKey = Guid.NewGuid().ToString();
        var workingSetUserDataValue = Guid.NewGuid().ToString();
        var workingSetTelemetryMetadata = new OutgoingTelemetryMetadata();
        workingSetTelemetryMetadata.UserData.Add(workingSetUserDataKey, workingSetUserDataValue);
        await memMonService.SendTelemetryAsync(new WorkingSetTelemetry() { workingSet = 1 }, workingSetTelemetryMetadata);

        var managedMemoryCorrelationId = Guid.NewGuid();
        var managedMemoryUserDataKey = Guid.NewGuid().ToString();
        var managedMemoryUserDataValue = Guid.NewGuid().ToString();
        var managedMemoryTelemetryMetadata = new OutgoingTelemetryMetadata() { CloudEvent = new CloudEvent(new Uri("test://mq")) };
        managedMemoryTelemetryMetadata.UserData.Add(managedMemoryUserDataKey, managedMemoryUserDataValue);
        await memMonService.SendTelemetryAsync(new ManagedMemoryTelemetry() { managedMemory = 2 }, managedMemoryTelemetryMetadata);

        // Wait for all receivers to receive some telemetry, or time out after a while.
        await Task.WhenAll(
            memmonClient.WorkingSetTelemetryReceivedTcs.Task,
            memmonClient.MemoryStatsTelemetryReceivedTcs.Task,
            memmonClient.ManagedMemoryTelemetryReceivedTcs.Task).WaitAsync(TimeSpan.FromSeconds(30));

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
        Assert.NotNull(memStatsMD.CloudEvent);
        Assert.Equal("1.0", memStatsMD.CloudEvent.SpecVersion);
        Assert.Equal("test://mq/", memStatsMD.CloudEvent.Source!.ToString());
        Assert.Equal("ms.aio.telemetry", memStatsMD.CloudEvent.Type);
        Assert.Equal($"rpc/samples/dtmi:akri:samples:memmon;1/{mqttSender.ClientId}/memoryStats", memStatsMD.CloudEvent.Subject);
        //Assert.Equal("1.0", memStatsMD.CloudEvent.DataSchema);
        Assert.Equal("application/avro", memStatsMD.CloudEvent.DataContentType);
        Assert.True(DateTime.TryParse(memStatsMD.CloudEvent.Time!.Value.ToString("o"), out DateTime _));
        Assert.True(Guid.TryParse(memStatsMD.CloudEvent.Id, out Guid _));
        Assert.Equal(mqttSender.ClientId, memStatsMD.SenderId);


        var managedMemoryMD = memmonClient.ReceivedManagedMemoryTelemetryMetadata[0];
        Assert.NotNull(managedMemoryMD);
        Assert.NotNull(managedMemoryMD.UserData);
        Assert.Equal(8, managedMemoryMD.UserData.Count);
        Assert.Equal("1.0", managedMemoryMD.CloudEvent!.SpecVersion);
        Assert.Equal("test://mq/", managedMemoryMD.CloudEvent.Source!.ToString());
        Assert.Equal("ms.aio.telemetry", managedMemoryMD.CloudEvent.Type);
        Assert.Equal($"rpc/samples/dtmi:akri:samples:memmon;1/{mqttSender.ClientId}/managedMemory", managedMemoryMD.CloudEvent.Subject);
        //Assert.Equal("1.0", managedMemoryMD.CloudEvent.DataSchema);
        Assert.Equal("application/avro", managedMemoryMD.CloudEvent.DataContentType);
        Assert.True(DateTime.TryParse(managedMemoryMD.CloudEvent.Time!.Value.ToString("o"), out DateTime _));
        Assert.True(Guid.TryParse(managedMemoryMD.CloudEvent.Id, out Guid _));
        Assert.Equal(mqttSender.ClientId, managedMemoryMD.SenderId);


        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData.ContainsKey(memoryStatsUserDataKey));
        Assert.Equal(memoryStatsUserDataValue, memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData[memoryStatsUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, memoryStatsTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].Timestamp!));



        Assert.NotNull(memmonClient.ReceivedMemoryStatsTelemetryMetadata[0].UserData);
        Assert.False(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData.ContainsKey("dataschema"));
        //Assert.Equal("TODO", memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData["dataschema"]);


        Assert.NotNull(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData.ContainsKey(workingSetUserDataKey));
        Assert.Equal(workingSetUserDataValue, memmonClient.ReceivedWorkingSetTelemetryMetadata[0].UserData[workingSetUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, workingSetTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedWorkingSetTelemetryMetadata[0].Timestamp!));

        Assert.NotNull(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData);
        Assert.True(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData.ContainsKey(managedMemoryUserDataKey));
        Assert.Equal(managedMemoryUserDataValue, memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].UserData[managedMemoryUserDataKey]);
        Assert.NotNull(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp);
        Assert.Equal(0, managedMemoryTelemetryMetadata.Timestamp.CompareTo(memmonClient.ReceivedManagedMemoryTelemetryMetadata[0].Timestamp!));
    }

    [Fact]
    public async Task Commands()
    {
        string invokerId = "test-invoker-" + Guid.NewGuid();
        await using MqttSessionClient mqttReceiver = await ClientFactory.CreateSessionClientFromEnvAsync(invokerId);
        await using MemmonClient memmonClient = new(mqttReceiver);

        string executorId = "test-executor-" + Guid.NewGuid();
        await using MqttSessionClient mqttSender = await ClientFactory.CreateSessionClientFromEnvAsync(executorId);
        await using MemMonService memMonService = new(mqttSender);
        await memmonClient.StartAsync();
        await memMonService.StartAsync();

        var resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { diagnosticsMode = Enum_GetRuntimeStats_Request.full }, commandTimeout: TimeSpan.FromSeconds(30));

        Assert.NotNull(resp);

        var startResp = await memmonClient.StartTelemetryAsync(executorId, new StartTelemetryRequestPayload() { interval = 4 });

        resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { diagnosticsMode = Enum_GetRuntimeStats_Request.full }, commandTimeout: TimeSpan.FromSeconds(30));
        Assert.Equal("4", resp.diagnosticResults["interval"]);
        Assert.Equal("True", resp.diagnosticResults["enabled"]);

        await memmonClient.StopTelemetryAsync(executorId);
        resp = await memmonClient.GetRuntimeStatsAsync(executorId, new GetRuntimeStatsRequestPayload() { diagnosticsMode = Enum_GetRuntimeStats_Request.full }, commandTimeout: TimeSpan.FromSeconds(30));
        Assert.Equal("False", resp.diagnosticResults["enabled"]);
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
