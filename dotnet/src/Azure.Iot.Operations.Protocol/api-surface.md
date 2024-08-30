# Azure.Iot.Operations.Protocol API surface

```csharp
namespace Azure.Iot.Operations.Protocol {
    public interface IMqttPubSubClient : IAsyncDisposable {
        string ClientId { get; }
        MqttProtocolVersion ProtocolVersion { get; }
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;
        ValueTask DisposeAsync(bool disposing);
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);
        Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);
        Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);
    }
    public class OrderedAckMqttClient : IMqttPubSubClient, IAsyncDisposable {
        public OrderedAckMqttClient(IMqttClient mqttNetClient);
        public string ClientId { get; }
        public bool IsConnected { get; }
        public MqttProtocolVersion ProtocolVersion { get; }
        public IMqttClient UnderlyingMqttClient { get; }
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;
        public virtual Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default);
        public virtual Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default);
        public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;
        public virtual Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default);
        public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;
        public virtual ValueTask DisposeAsync();
        public virtual ValueTask DisposeAsync(bool disposing);
        public virtual Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);
        public Task ReconnectAsync(CancellationToken cancellationToken = default);
        public virtual Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);
        public virtual Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);
    }
    public interface IDelayableQueueItem {
        bool IsReady();
        void MarkAsReady();
    }
    public interface IPayloadSerializer {
        int CharacterDataFormatIndicator { get; }
        string ContentType { get; }
        T FromBytes<T>(byte[]? payload) where T : class;
        byte[]? ToBytes<T>(T? payload) where T : class;
    }
    public interface IWallClock {
        DateTime UtcNow { get; }
        CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);
        Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken);
    }
    public static class AkriSystemProperties {
        public const string CommandInvokerId = "__invId";
        public const string IsApplicationError = "__apErr";
        public const string ReservedPrefix = "__";
        public const string Status = "__stat";
        public const string StatusMessage = "__stMsg";
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandBehaviorAttribute : Attribute {
        public CommandBehaviorAttribute(bool idempotent = false, string cacheableDuration = "PT0H0M0S");
        public string CacheableDuration { get; set; }
        public bool IsIdempotent { get; set; }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandTopicAttribute : Attribute {
        public CommandTopicAttribute(string topic);
        public string RequestTopic { get; set; }
    }
    public class HybridLogicalClock {
        public HybridLogicalClock(DateTime? timestamp = null, int counter = 0, string? nodeId = null);
        public HybridLogicalClock(HybridLogicalClock other);
        public int Counter { get; set; }
        public string NodeId { get; }
        public DateTime Timestamp { get; set; }
        public static HybridLogicalClock GetInstance();
        public int CompareTo(HybridLogicalClock other);
        public void Update(HybridLogicalClock? other = null, TimeSpan? maxClockDrift = null);
        public override bool Equals(object? obj);
        public override int GetHashCode();
        public override string ToString();
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class ModelIdAttribute : Attribute {
        public ModelIdAttribute(string id);
        public string Id { get; set; }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceGroupIdAttribute : Attribute {
        public ServiceGroupIdAttribute(string id);
        public string Id { get; set; }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class TelemetryTopicAttribute : Attribute {
        public TelemetryTopicAttribute(string topic);
        public string Topic { get; set; }
    }
    public class WallClock : IWallClock {
        public WallClock();
        public DateTime UtcNow { get; }
        public CancellationTokenSource CreateCancellationTokenSource(TimeSpan delay);
        public Task<T> WaitAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken);
    }
    public static class GuidExtensions {
        public static bool TryParseBytes(byte[] bytes, out Guid? result);
    }
    public static class MqttFactoryExtensions {
        public static OrderedAckMqttClient CreateOrderedAckMqttClient(this MqttFactory factory, IMqttNetLogger? logger = null);
    }
    public enum AkriMqttErrorKind {
        ConfigurationInvalid = 0,
        ArgumentInvalid = 1,
        HeaderMissing = 2,
        HeaderInvalid = 3,
        PayloadInvalid = 4,
        StateInvalid = 5,
        InternalLogicError = 6,
        Timeout = 7,
        Cancellation = 8,
        InvocationException = 9,
        ExecutionException = 10,
        UnknownError = 11,
        MqttError = 12,
    }
    public class AkriMqttException : Exception {
        public string? CommandName { get; }
        public Guid? CorrelationId { get; }
        public string? HeaderName { get; }
        public string? HeaderValue { get; }
        public int? HttpStatusCode { get; }
        public required bool InApplication { get; init; }
        public required bool IsRemote { get; init; }
        public required bool IsShallow { get; init; }
        public required AkriMqttErrorKind Kind { get; init; }
        public string? PropertyName { get; }
        public object? PropertyValue { get; }
        public string? TimeoutName { get; }
        public TimeSpan? TimeoutValue { get; }
        public static AkriMqttException GetPayloadInvalidException();
    }
    public class HybridLogicalClockException : Exception {
        public HybridLogicalClockException();
        public HybridLogicalClockException(string message);
    }
}
namespace Azure.Iot.Operations.Protocol.Connection {
    public class MqttConnectionSettings {
        public MqttConnectionSettings(string hostname);
        protected MqttConnectionSettings(IDictionary<string, string> connectionSettings, bool validateOptionalSettings, bool isSettingFromConnStr = false);
        public string? CaFile { get; init; }
        public bool CaRequireRevocationCheck { get; init; }
        public string? CertFile { get; init; }
        public bool CleanStart { get; init; }
        public X509Certificate2? ClientCertificate { get; set; }
        public string? ClientId { get; init; }
        public TimeSpan? ConnectionTimeout { get; set; }
        public string HostName { get; init; }
        public TimeSpan KeepAlive { get; init; }
        public string? KeyFile { get; init; }
        public string? KeyFilePassword { get; init; }
        public string? ModelId { get; init; }
        public string? Password { get; init; }
        public string? PasswordFile { get; init; }
        public string? SatAuthFile { get; init; }
        public TimeSpan SessionExpiry { get; set; }
        public int TcpPort { get; init; }
        public X509Certificate2Collection? TrustChain { get; set; }
        public string? Username { get; init; }
        public bool UseTls { get; init; }
        public static MqttConnectionSettings FromConnectionString(string connectionString);
        public static MqttConnectionSettings FromEnvVars();
        protected static bool GetBooleanValue(IDictionary<string, string> dict, string propertyName, bool defaultValue = false);
        protected static int GetPositiveIntValueOrDefault(IDictionary<string, string> dict, string propertyName, int defaultValue = 0);
        protected static string? GetStringValue(IDictionary<string, string> dict, string propertyName);
        protected static TimeSpan GetTimeSpanValue(IDictionary<string, string> dict, string propertyName, TimeSpan defaultValue = default);
        protected void ValidateMqttSettings(bool validateOptionalSettings);
        public override string ToString();
    }
    public class MqttNetTraceLogger {
        public MqttNetTraceLogger();
        public static MqttNetEventLogger CreateTraceLogger();
    }
    public static class MqttClientExtensions {
        public static Task<MqttClientConnectResult> ConnectAsync(this IMqttClient client, MqttConnectionSettings mqttConnectionSettings, CancellationToken cancellationToken = default);
    }
    public static class MqttNetExtensions {
        public static MqttClientOptionsBuilder WithMqttConnectionSettings(this MqttClientOptionsBuilder builder, MqttConnectionSettings cs);
    }
}
namespace Azure.Iot.Operations.Protocol.Retry {
    public interface IRetryPolicy {
        bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
    }
    public class ExponentialBackoffRetryPolicy : IRetryPolicy {
        public ExponentialBackoffRetryPolicy(uint maxRetries, TimeSpan maxWait, bool useJitter = true);
        public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
        protected TimeSpan UpdateWithJitter(double baseTimeMs);
    }
    public class NoRetryPolicy : IRetryPolicy {
        public NoRetryPolicy();
        public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
    }
    public class RetryExpiredException : Exception {
        public RetryExpiredException();
        public RetryExpiredException(string? message);
        public RetryExpiredException(string? message, Exception? innerException);
    }
}
namespace Azure.Iot.Operations.Protocol.RPC {
    public abstract class CommandExecutor<TReq, TResp> where TReq : class where TResp : class : IAsyncDisposable {
        public CommandExecutor(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer);
        public TimeSpan CacheableDuration { get; init; }
        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }
        public TimeSpan ExecutionTimeout { get; set; }
        public string? ExecutorId { get; set; }
        public bool IsIdempotent { get; init; }
        public string ModelId { get; init; }
        public required Func<ExtendedRequest<TReq>, CancellationToken, Task<ExtendedResponse<TResp>>> OnCommandReceived { get; set; }
        public string RequestTopicPattern { get; init; }
        public string ServiceGroupId { get; init; }
        public string? TopicNamespace { get; set; }
        public virtual ValueTask DisposeAsync();
        public virtual ValueTask DisposeAsync(bool disposing);
        public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default);
        public Task StopAsync(CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public abstract class CommandInvoker<TReq, TResp> where TReq : class where TResp : class : IAsyncDisposable {
        public CommandInvoker(IMqttPubSubClient mqttClient, string commandName, IPayloadSerializer serializer);
        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }
        public TimeSpan DefaultCommandTimeout { get; init; }
        public Func<string, string>? GetResponseTopic { get; init; }
        public string ModelId { get; init; }
        public string RequestTopicPattern { get; init; }
        public string? ResponseTopicPrefix { get; init; }
        public string? ResponseTopicSuffix { get; init; }
        public string? TopicNamespace { get; set; }
        public ValueTask DisposeAsync();
        public ValueTask DisposeAsync(bool disposing);
        public Task<ExtendedResponse<TResp>> InvokeCommandAsync(string executorId, TReq request, CommandRequestMetadata? metadata = null, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public class CommandRequestMetadata {
        public CommandRequestMetadata();
        public Guid CorrelationId { get; }
        public HybridLogicalClock? FencingToken { get; set; }
        public string? InvokerClientId { get; }
        public HybridLogicalClock? Timestamp { get; }
        public Dictionary<string, string> UserData { get; }
    }
    public class CommandResponseMetadata {
        public CommandResponseMetadata();
        public Guid? CorrelationId { get; }
        public HybridLogicalClock? Timestamp { get; }
        public Dictionary<string, string> UserData { get; set; }
        public void MarshalTo(MqttApplicationMessageBuilder messageBuilder);
    }
    public struct ExtendedRequest<TReq> where TReq : class {
        public TReq Request { get; set; }
        public CommandRequestMetadata RequestMetadata { get; set; }
    }
    public struct ExtendedResponse<TResp> where TResp : class {
        public TResp Response { get; set; }
        public CommandResponseMetadata? ResponseMetadata { get; set; }
        public static ExtendedResponse<TResp> CreateFromResponse(TResp response);
    }
    public struct RpcCallAsync<TResp> where TResp : class {
        public RpcCallAsync(Task<ExtendedResponse<TResp>> task, Guid requestCorrelationData);
        public Task<ExtendedResponse<TResp>> ExtendedAsync { get; }
        public Guid RequestCorrelationData { get; }
        public TaskAwaiter<TResp> GetAwaiter();
        public Task<ExtendedResponse<TResp>> WithMetadata();
    }
    public enum CommandStatusCode {
        OK = 200,
        NoContent = 204,
        BadRequest = 400,
        RequestTimeout = 408,
        UnsupportedMediaType = 415,
        UnprocessableContent = 422,
        InternalServerError = 500,
        ServiceUnavailable = 503,
    }
    public class InvocationException : Exception {
        public InvocationException(string? statusMessage = null, string? propertyName = null, string? propertyValue = null);
        public string? InvalidPropertyName { get; }
        public string? InvalidPropertyValue { get; }
    }
}
namespace Azure.Iot.Operations.Protocol.Session {
    public class MqttSessionClient : OrderedAckMqttClient {
        public MqttSessionClient(MqttSessionClientOptions? sessionClientOptions = null);
        public MqttClientOptions? ClientOptions { get; }
        public override Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default);
        public override Task<MqttClientConnectResult> ConnectAsync(MqttConnectionSettings settings, CancellationToken cancellationToken = default);
        public override Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default);
        public override ValueTask DisposeAsync();
        public override Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);
        public event Func<MqttClientDisconnectedEventArgs, Task>? SessionLostAsync;
        public override Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);
        public override Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);
    }
    public class MqttSessionClientOptions {
        public MqttSessionClientOptions();
        public IRetryPolicy ConnectionRetryPolicy { get; set; }
        public bool EnableMqttLogging { get; set; }
        public uint MaxPendingMessages { get; set; }
        public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; }
        public bool RetryOnFirstConnect { get; set; }
    }
}
namespace Azure.Iot.Operations.Protocol.Session.Exceptions {
    public class MessagePurgedFromQueueException : Exception {
        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy?? messagePurgeStrategy = null);
        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy?? messagePurgeStrategy, string? message);
        public MessagePurgedFromQueueException(MqttPendingMessagesOverflowStrategy?? messagePurgeStrategy, string? message, Exception? innerException);
        public MqttPendingMessagesOverflowStrategy?? MessagePurgeStrategy { get; }
    }
    public class MqttSessionExpiredException : Exception {
        public MqttSessionExpiredException();
        public MqttSessionExpiredException(string? message);
        public MqttSessionExpiredException(string? message, Exception? innerException);
    }
}
namespace Azure.Iot.Operations.Protocol.Telemetry {
    public class CloudEvent {
        public CloudEvent(Uri source, string type = "ms.aio.telemetry", string specversion = "1.0");
        public string? DataContentType { get; }
        public string? DataSchema { get; set; }
        public string? Id { get; }
        public Uri? Source { get; }
        public string SpecVersion { get; }
        public string? Subject { get; }
        public DateTime? Time { get; }
        public string Type { get; }
    }
    public class IncomingTelemetryMetadata {
        public CloudEvent? CloudEvent { get; }
        public uint PacketId { get; }
        public HybridLogicalClock? Timestamp { get; }
        public Dictionary<string, string> UserData { get; }
    }
    public static class MqttApplicationMessageBuilderExtension {
        public static MqttApplicationMessageBuilder WithCloudEvents(this MqttApplicationMessageBuilder mb, CloudEvent md);
        public static MqttApplicationMessageBuilder WithMetadata(this MqttApplicationMessageBuilder mb, OutgoingTelemetryMetadata md);
    }
    public class OutgoingTelemetryMetadata {
        public OutgoingTelemetryMetadata();
        public CloudEvent? CloudEvent { get; set; }
        public HybridLogicalClock Timestamp { get; }
        public Dictionary<string, string> UserData { get; }
    }
    public abstract class TelemetryReceiver<T> where T : class : IAsyncDisposable {
        public TelemetryReceiver(IMqttPubSubClient mqttClient, string? telemetryName, IPayloadSerializer serializer);
        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }
        public string ModelId { get; init; }
        public Func<string, T, IncomingTelemetryMetadata, Task>? OnTelemetryReceived { get; init; }
        public string ServiceGroupId { get; init; }
        public string? TopicNamespace { get; set; }
        public string TopicPattern { get; init; }
        public virtual ValueTask DisposeAsync();
        public virtual ValueTask DisposeAsync(bool disposing);
        public Task StartAsync(CancellationToken cancellationToken = default);
        public Task StopAsync(CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
    public abstract class TelemetrySender<T> where T : class : IAsyncDisposable {
        public TelemetrySender(IMqttPubSubClient mqttClient, string? telemetryName, IPayloadSerializer serializer);
        public Dictionary<string, string>? CustomTopicTokenMap { get; init; }
        public TimeSpan DefaultMessageExpiryInterval { get; init; }
        public string ModelId { get; init; }
        public string? TopicNamespace { get; set; }
        public string TopicPattern { get; init; }
        public virtual ValueTask DisposeAsync();
        public virtual ValueTask DisposeAsync(bool disposing);
        public Task SendTelemetryAsync(T telemetry, MqttQualityOfServiceLevel qos = 1, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default);
        public Task SendTelemetryAsync(T telemetry, OutgoingTelemetryMetadata metadata, MqttQualityOfServiceLevel qos = 1, TimeSpan? messageExpiryInterval = null, CancellationToken cancellationToken = default);
        protected virtual ValueTask DisposeAsyncCore(bool disposing);
    }
}
```
