# Azure.Iot.Operations.Services

```csharp
namespace Azure.Iot.Operations.Services.LeaderElection {
    public class LeaderElectionClient : IAsyncDisposable {
        public LeaderElectionClient(IMqttPubSubClient mqttClient, string leadershipPositionId, string? candidateName = null);
        public LeaderElectionAutomaticRenewalOptions AutomaticRenewalOptions { get; set; }
        public string CandidateName { get; }
        public CampaignResponse? LastKnownCampaignResult { get; }
        public virtual Task<CampaignResponse> CampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default);
        public ValueTask DisposeAsync();
        public ValueTask DisposeAsync(bool disposing);
        public virtual Task<GetCurrentLeaderResponse> GetCurrentLeaderAsync(CancellationToken cancellationToken = default);
        public event Func<object?, LeadershipChangeEventArgs, Task>? LeadershipChangeEventReceivedAsync;
        public virtual Task ObserveLeadershipChangesAsync(ObserveLeadershipChangesRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task<ResignationResponse> ResignAsync(ResignationRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task<CampaignResponse> TryCampaignAsync(TimeSpan electionTerm, CampaignRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task UnobserveLeadershipChangesAsync(CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public class CampaignRequestOptions {
        public CampaignRequestOptions();
        public string? SessionId { get; set; }
    }
    public class LeaderElectionAutomaticRenewalOptions {
        public LeaderElectionAutomaticRenewalOptions();
        public bool AutomaticRenewal { get; set; }
        public TimeSpan ElectionTerm { get; set; }
        public TimeSpan RenewalPeriod { get; set; }
    }
    public class ObserveLeadershipChangesRequestOptions {
        public ObserveLeadershipChangesRequestOptions();
        public bool GetNewLeader { get; set; }
        public bool GetPreviousLeader { get; set; }
    }
    public class ResignationRequestOptions {
        public ResignationRequestOptions();
        public bool CancelAutomaticRenewal { get; set; }
        public string? SessionId { get; set; }
    }
    public class CampaignResponse {
        public HybridLogicalClock? FencingToken { get; }
        public bool IsLeader { get; }
        public LeaderElectionCandidate? LastKnownLeader { get; }
    }
    public class GetCurrentLeaderResponse {
        public LeaderElectionCandidate? CurrentLeader { get; set; }
    }
    public class LeaderElectionCandidate : IEquatable<LeaderElectionCandidate> {
        public byte[] Bytes { get; }
        public static implicit operator LeaderElectionCandidate?(string? value);
        public bool Equals(LeaderElectionCandidate? other);
        public string GetString();
        public override bool Equals(object? other);
        public override int GetHashCode();
    }
    public sealed class LeadershipChangeEventArgs : EventArgs {
        public LeaderElectionCandidate? NewLeader { get; }
        public LeadershipPositionState NewState { get; }
        public LeaderElectionCandidate? PreviousLeader { get; }
    }
    public class ResignationResponse {
        public bool Success { get; }
    }
    public enum LeadershipPositionState {
        LeaderElected = 0,
        NoLeader = 1,
    }
}
namespace Azure.Iot.Operations.Services.LeasedLock {
    public class LeasedLockClient : IAsyncDisposable {
        public LeasedLockClient(IMqttPubSubClient mqttClient, string lockName, string? lockHolderName = null);
        public LeasedLockClient(IStateStoreClient stateStoreClient, string lockName, string lockHolderName);
        public LeasedLockAutomaticRenewalOptions AutomaticRenewalOptions { get; set; }
        public string LockHolderName { get; }
        public AcquireLockResponse? MostRecentAcquireLockResponse { get; }
        public virtual Task<AcquireLockResponse> AcquireLockAsync(TimeSpan leaseDuration, AcquireLockRequestOptions? options = null, CancellationToken cancellationToken = default);
        public ValueTask DisposeAsync();
        public ValueTask DisposeAsync(bool disposing);
        public virtual Task<GetLockHolderResponse> GetLockHolderAsync(CancellationToken cancellationToken = default);
        public event Func<object?, LockChangeEventArgs, Task>? LockChangeEventReceivedAsync;
        public virtual Task ObserveLockAsync(ObserveLockRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task<ReleaseLockResponse> ReleaseLockAsync(ReleaseLockRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task<AcquireLockResponse> TryAcquireLockAsync(TimeSpan leaseDuration, AcquireLockRequestOptions? options = null, CancellationToken cancellationToken = default);
        public virtual Task UnobserveLockAsync(CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public class AcquireLockRequestOptions {
        public AcquireLockRequestOptions();
        public string? SessionId { get; set; }
    }
    public class LeasedLockAutomaticRenewalOptions {
        public LeasedLockAutomaticRenewalOptions();
        public bool AutomaticRenewal { get; set; }
        public TimeSpan LeaseTermLength { get; set; }
        public TimeSpan RenewalPeriod { get; set; }
    }
    public class ObserveLockRequestOptions {
        public ObserveLockRequestOptions();
        public bool GetNewValue { get; set; }
        public bool GetPreviousValue { get; set; }
    }
    public class ReleaseLockRequestOptions {
        public ReleaseLockRequestOptions();
        public bool CancelAutomaticRenewal { get; set; }
        public string? SessionId { get; set; }
    }
    public class AcquireLockResponse {
        public HybridLogicalClock? FencingToken { get; }
        public LeasedLockHolder? LastKnownOwner { get; }
        public bool Success { get; }
    }
    public class GetLockHolderResponse {
        public LeasedLockHolder? LockHolder { get; }
    }
    public class LeasedLockHolder : IEquatable<LeasedLockHolder> {
        public byte[] Bytes { get; }
        public static implicit operator LeasedLockHolder(string value);
        public bool Equals(LeasedLockHolder? other);
        public string GetString();
        public override bool Equals(object? other);
        public override int GetHashCode();
    }
    public sealed class LockChangeEventArgs : EventArgs {
        public LeasedLockHolder? NewLockHolder { get; }
        public LockState NewState { get; }
        public LeasedLockHolder? PreviousLockHolder { get; }
    }
    public class ReleaseLockResponse {
        public bool Success { get; }
    }
    public enum LockState {
        Acquired = 0,
        Released = 1,
    }
}
namespace Azure.Iot.Operations.Services.SchemaRegistry {
    public interface ISchemaRegistryClient {
        Task<Object_Ms_Adr_SchemaRegistry_Schema__1> GetAsync(string schemaId, string version = "1.0.0", TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        Task<Object_Ms_Adr_SchemaRegistry_Schema__1> PutAsync(string schemaContent, Enum_Ms_Adr_SchemaRegistry_Format__1 schemaFormat, Enum_Ms_Adr_SchemaRegistry_SchemaType__1 schemaType = MessageSchema, string version = "1.0.0", Dictionary<string, string> tags = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
    public class SchemaRegistryClient : ISchemaRegistryClient, IAsyncDisposable {
        public SchemaRegistryClient(IMqttPubSubClient pubSubClient);
        public ValueTask DisposeAsync();
        public Task<Object_Ms_Adr_SchemaRegistry_Schema__1> GetAsync(string schemaId, string version = "1.0.0", TimeSpan? timeout = null, CancellationToken cancellationToken = default);
        public Task<Object_Ms_Adr_SchemaRegistry_Schema__1> PutAsync(string schemaContent, Enum_Ms_Adr_SchemaRegistry_Format__1 schemaFormat, Enum_Ms_Adr_SchemaRegistry_SchemaType__1 schemaType = MessageSchema, string version = "1.0.0", Dictionary<string, string> tags = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
    public class EmptyJson {
        public EmptyJson();
    }
    public class Utf8JsonSerializer {
        public Utf8JsonSerializer();
        protected static readonly JsonSerializerOptions jsonSerializerOptions;
        public int CharacterDataFormatIndicator { get; }
        public string ContentType { get; }
        public T FromBytes<T>(byte[]? payload, string? contentType = null, int? payloadFormatIndicator = null) where T : class;
        public SerializedPayloadContext ToBytes<T>(T? payload) where T : class;
    }
}
namespace Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1 {
    public class GetCommandRequest {
        public GetCommandRequest();
        [JsonPropertyName("getSchemaRequest")]
        [JsonIgnore]
        public Object_Get_Request GetSchemaRequest { get; set; }
    }
    public class GetCommandResponse {
        public GetCommandResponse();
        [JsonPropertyName("schema")]
        [JsonIgnore]
        public Object_Ms_Adr_SchemaRegistry_Schema__1 Schema { get; set; }
    }
    public class Object_Get_Request {
        public Object_Get_Request();
        [JsonPropertyName("name")]
        [JsonIgnore]
        public string? Name { get; set; }
        [JsonPropertyName("version")]
        [JsonIgnore]
        public string? Version { get; set; }
    }
    public class Object_Ms_Adr_SchemaRegistry_Schema__1 {
        public Object_Ms_Adr_SchemaRegistry_Schema__1();
        [JsonPropertyName("description")]
        [JsonIgnore]
        public string? Description { get; set; }
        [JsonPropertyName("displayName")]
        [JsonIgnore]
        public string? DisplayName { get; set; }
        [JsonPropertyName("format")]
        [JsonIgnore]
        public Enum_Ms_Adr_SchemaRegistry_Format__1? Format { get; set; }
        [JsonPropertyName("hash")]
        [JsonIgnore]
        public string? Hash { get; set; }
        [JsonPropertyName("name")]
        [JsonIgnore]
        public string? Name { get; set; }
        [JsonPropertyName("namespace")]
        [JsonIgnore]
        public string? Namespace { get; set; }
        [JsonPropertyName("schemaContent")]
        [JsonIgnore]
        public string? SchemaContent { get; set; }
        [JsonPropertyName("schemaType")]
        [JsonIgnore]
        public Enum_Ms_Adr_SchemaRegistry_SchemaType__1? SchemaType { get; set; }
        [JsonPropertyName("tags")]
        [JsonIgnore]
        public Dictionary<string, string>? Tags { get; set; }
        [JsonPropertyName("version")]
        [JsonIgnore]
        public string? Version { get; set; }
    }
    public class Object_Put_Request {
        public Object_Put_Request();
        [JsonPropertyName("description")]
        [JsonIgnore]
        public string? Description { get; set; }
        [JsonPropertyName("displayName")]
        [JsonIgnore]
        public string? DisplayName { get; set; }
        [JsonPropertyName("format")]
        [JsonIgnore]
        public Enum_Ms_Adr_SchemaRegistry_Format__1? Format { get; set; }
        [JsonPropertyName("schemaContent")]
        [JsonIgnore]
        public string? SchemaContent { get; set; }
        [JsonPropertyName("schemaType")]
        [JsonIgnore]
        public Enum_Ms_Adr_SchemaRegistry_SchemaType__1? SchemaType { get; set; }
        [JsonPropertyName("tags")]
        [JsonIgnore]
        public Dictionary<string, string>? Tags { get; set; }
        [JsonPropertyName("version")]
        [JsonIgnore]
        public string? Version { get; set; }
    }
    public class PutCommandRequest {
        public PutCommandRequest();
        [JsonPropertyName("putSchemaRequest")]
        [JsonIgnore]
        public Object_Put_Request PutSchemaRequest { get; set; }
    }
    public class PutCommandResponse {
        public PutCommandResponse();
        [JsonPropertyName("schema")]
        [JsonIgnore]
        public Object_Ms_Adr_SchemaRegistry_Schema__1 Schema { get; set; }
    }
    public static class SchemaRegistry {
        public abstract class Client : IAsyncDisposable {
            public Client(IMqttPubSubClient mqttClient);
            public Dictionary<string, string> CustomTopicTokenMap { get; }
            public GetCommandInvoker GetCommandInvoker { get; }
            public PutCommandInvoker PutCommandInvoker { get; }
            public ValueTask DisposeAsync();
            public ValueTask DisposeAsync(bool disposing);
            public RpcCallAsync<GetCommandResponse> GetAsync(GetCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);
            public RpcCallAsync<PutCommandResponse> PutAsync(PutCommandRequest request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);
        }
        public class GetCommandExecutor : CommandExecutor<GetCommandRequest, GetCommandResponse> {
        }
        public class GetCommandInvoker : CommandInvoker<GetCommandRequest, GetCommandResponse> {
        }
        public class PutCommandExecutor : CommandExecutor<PutCommandRequest, PutCommandResponse> {
        }
        public class PutCommandInvoker : CommandInvoker<PutCommandRequest, PutCommandResponse> {
        }
        public abstract class Service : IAsyncDisposable {
            public Service(IMqttPubSubClient mqttClient);
            public Dictionary<string, string> CustomTopicTokenMap { get; }
            public GetCommandExecutor GetCommandExecutor { get; }
            public PutCommandExecutor PutCommandExecutor { get; }
            public ValueTask DisposeAsync();
            public ValueTask DisposeAsync(bool disposing);
            public abstract Task<ExtendedResponse<GetCommandResponse>> GetAsync(GetCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);
            public abstract Task<ExtendedResponse<PutCommandResponse>> PutAsync(PutCommandRequest request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);
            public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default);
            public Task StopAsync(CancellationToken cancellationToken = default);
        }
    }
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum Enum_Ms_Adr_SchemaRegistry_Format__1 {
        [EnumMember]
        Delta1 = 0,
        [EnumMember]
        JsonSchemaDraft07 = 1,
    }
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum Enum_Ms_Adr_SchemaRegistry_SchemaType__1 {
        [EnumMember]
        MessageSchema = 0,
    }
}
namespace Azure.Iot.Operations.Services.StateStore {
    public interface IStateStoreClient : IAsyncDisposable {
        Task<StateStoreDeleteResponse> DeleteAsync(StateStoreKey key, StateStoreDeleteRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        ValueTask DisposeAsync(bool disposing);
        Task<StateStoreGetResponse> GetAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        public event Func<object?, KeyChangeMessageReceivedEventArgs, Task>? KeyChangeMessageReceivedAsync;
        Task ObserveAsync(StateStoreKey key, StateStoreObserveRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        Task<StateStoreSetResponse> SetAsync(StateStoreKey key, StateStoreValue value, StateStoreSetRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        Task UnobserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
    }
    public class StateStoreClient : IAsyncDisposable, IStateStoreClient {
        public StateStoreClient(IMqttPubSubClient mqttClient);
        public virtual Task<StateStoreDeleteResponse> DeleteAsync(StateStoreKey key, StateStoreDeleteRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        public ValueTask DisposeAsync();
        public ValueTask DisposeAsync(bool disposing);
        public virtual Task<StateStoreGetResponse> GetAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        public event Func<object?, KeyChangeMessageReceivedEventArgs, Task>? KeyChangeMessageReceivedAsync;
        public virtual Task ObserveAsync(StateStoreKey key, StateStoreObserveRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        public virtual Task<StateStoreSetResponse> SetAsync(StateStoreKey key, StateStoreValue value, StateStoreSetRequestOptions? options = null, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        public virtual Task UnobserveAsync(StateStoreKey key, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public class StateStoreDeleteRequestOptions {
        public StateStoreDeleteRequestOptions();
        public HybridLogicalClock? FencingToken { get; set; }
        public StateStoreValue? OnlyDeleteIfValueEquals { get; set; }
    }
    public class StateStoreObserveRequestOptions {
        public StateStoreObserveRequestOptions();
        public bool GetNewValue { get; set; }
        public bool GetPreviousValue { get; set; }
    }
    public class StateStoreSetRequestOptions {
        public StateStoreSetRequestOptions();
        public SetCondition Condition { get; set; }
        public TimeSpan? ExpiryTime { get; set; }
        public HybridLogicalClock? FencingToken { get; set; }
    }
    public sealed class KeyChangeMessageReceivedEventArgs : EventArgs {
        public StateStoreKey ChangedKey { get; }
        public KeyState NewState { get; }
        public StateStoreValue? NewValue { get; }
        public StateStoreValue? PreviousValue { get; }
        public HybridLogicalClock? Version { get; set; }
    }
    public static class MqttTopicTokens {
        public const string CommandExecutorId = "{executorId}";
        public const string CommandInvokerId = "{invokerClientId}";
        public const string CommandName = "{commandName}";
        public const string CustomPrefix = "ex:";
        public const string ModelId = "{modelId}";
        public const string TelemetryName = "{telemetryName}";
        public const string TelemetrySenderId = "{senderId}";
    }
    public class PassthroughSerializer {
        public PassthroughSerializer();
        public int CharacterDataFormatIndicator { get; }
        public string ContentType { get; }
        public T FromBytes<T>(byte[]? payload, string? contentType = null, int? payloadFormatIndicator = null) where T : class;
        public SerializedPayloadContext ToBytes<T>(T? payload) where T : class;
    }
    public class StateStoreDeleteResponse {
        public int? DeletedItemsCount { get; }
    }
    public class StateStoreGetResponse : StateStoreResponse {
        public StateStoreValue? Value { get; }
    }
    public class StateStoreKey : StateStoreObject {
        public StateStoreKey(string key);
        public StateStoreKey(byte[] key);
        public StateStoreKey(Stream value);
        public StateStoreKey(Stream value, long length);
        public StateStoreKey(ArraySegment<byte> value);
        public StateStoreKey(IEnumerable<byte> value);
        public HybridLogicalClock? Version { get; }
        public static implicit operator StateStoreKey(string value);
    }
    public class StateStoreKeyPattern : StateStoreObject {
        public StateStoreKeyPattern(string key);
        public StateStoreKeyPattern(byte[] key);
        public StateStoreKeyPattern(Stream value);
        public StateStoreKeyPattern(Stream value, long length);
        public StateStoreKeyPattern(ArraySegment<byte> value);
        public StateStoreKeyPattern(IEnumerable<byte> value);
    }
    public abstract class StateStoreObject : IEquatable<StateStoreObject> {
        public StateStoreObject(string value);
        public StateStoreObject(byte[] value);
        public StateStoreObject(Stream value);
        public StateStoreObject(Stream value, long length);
        public StateStoreObject(ArraySegment<byte> value);
        public StateStoreObject(IEnumerable<byte> value);
        public byte[] Bytes { get; }
        public bool Equals(StateStoreObject? other);
        public string GetString();
        public override bool Equals(object? other);
        public override int GetHashCode();
    }
    public abstract class StateStoreResponse {
        public HybridLogicalClock? Version { get; set; }
    }
    public class StateStoreSetResponse : StateStoreResponse {
        public StateStoreValue? PreviousValue { get; }
        public bool Success { get; }
    }
    public class StateStoreValue : StateStoreObject {
        public StateStoreValue(string value);
        public StateStoreValue(byte[] value);
        public StateStoreValue(Stream value);
        public StateStoreValue(Stream value, long length);
        public StateStoreValue(ArraySegment<byte> value);
        public StateStoreValue(IEnumerable<byte> value);
        public static implicit operator StateStoreValue(string value);
    }
    public enum KeyState {
        Deleted = 0,
        Updated = 1,
    }
    public enum SetCondition {
        OnlyIfNotSet = 0,
        OnlyIfEqualOrNotSet = 1,
        Unconditional = 2,
    }
    public class StateStoreOperationException : Exception {
        public StateStoreOperationException(string message, Exception innerException);
        public StateStoreOperationException(string message);
    }
}
namespace Azure.Iot.Operations.Services.StateStore.dtmi_ms_aio_mq_StateStore__1 {
    public static class StateStore {
        public abstract class Client : IAsyncDisposable {
            public Client(IMqttPubSubClient mqttClient);
            public Dictionary<string, string> CustomTopicTokenMap { get; }
            public InvokeCommandInvoker InvokeCommandInvoker { get; }
            public ValueTask DisposeAsync();
            public ValueTask DisposeAsync(bool disposing);
            public RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);
        }
        public class InvokeCommandExecutor : CommandExecutor<byte[], byte[]> {
        }
        public class InvokeCommandInvoker : CommandInvoker<byte[], byte[]> {
        }
        public abstract class Service : IAsyncDisposable {
            public Service(IMqttPubSubClient mqttClient);
            public Dictionary<string, string> CustomTopicTokenMap { get; }
            public InvokeCommandExecutor InvokeCommandExecutor { get; }
            public ValueTask DisposeAsync();
            public ValueTask DisposeAsync(bool disposing);
            public abstract Task<ExtendedResponse<byte[]>> InvokeAsync(byte[] request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken);
            public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default);
            public Task StopAsync(CancellationToken cancellationToken = default);
        }
    }
}
namespace Azure.Iot.Operations.Services.StateStore.RESP3 {
    public class Resp3ProtocolException : Exception {
        public Resp3ProtocolException(string message);
        public Resp3ProtocolException(string? message, Exception? innerException);
    }
    public class Resp3SimpleErrorException : Exception {
        public Resp3SimpleErrorException(string errorDescription);
        public string ErrorDescription { get; set; }
    }
}
```