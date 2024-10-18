# Session Client

## Problem statement

Within an Azure IoT Operations ecosystem, there are various components that need to establish and maintain a reasonably stable MQTT session with an MQTT broker. Currently, every simple component (such as an RPC client or a DSS client) assumes this MQTT session is being maintained for them. This means that users of these components would need to write application-level logic to ensure that their connection and desired session state is maintained. While some users may prefer owning this logic, some would prefer if our client handled this for them.

While there are some managed clients that already exist in the respective MQTT client repos that meet some of our requirements, most of them have problems that would prevent us from taking a dependency on them as they currently are. For details on what those problems are for each language, see the appendix.

## Proposal

Create a new `IMqttClientPubSub` interface for all binders/envoys/RPC/DSS clients to accept instead of the current `IMqttClient` interface from the underlying MQTT library.

Create an `MqttSessionClient` concrete class that wraps our current languages' respective MQTT clients and manages the connection for the user while implementing the above `IMqttClientPubSub` interface. 

This `MqttSessionClient` would then be dependency injected into all the various components such as RPC clients and DSS clients and these clients would simply perform their publish/subscribe/unsubscribe operations without regarding the connection state.

This `MqttSessionClient` would allow us to not have to write duplicate connection management code in all our different clients. Finally, it would give us the opportunity to unify the MQTT client interface across languages via the `IMqttClientPubSub` interface that we define.

The primary goal for this session client is to meet our connection and session management needs in the various binders and RPC clients. We will consider trying to merge these session clients into the the respective MQTT client libraries, but only as a secondary goal.

## Test Strategy

For details on the proposed test strategy for this new `MqttSessionClient`, see [Session Client Testing](session-client-testing.md).

## Design Approach and Example APIs

### Core async interfaces  - Publish, Subscribe, Unsubscribe

The proposed MQTT Pub/Sub interface leverages the existing interface from the underlying unmanaged MQTT client. The interface includes `publish`, `subscribe` and `unsubscribe` operations, but omits `connect` and `Disconnect`. The reason for this omission is that any library that takes in an `IMqttPubSubClient` should rely on a different layer to connect/disconnect/handle disconnections.

For example:

```csharp
// Note that this binder doesn't need to make any calls to connect/disconnect since it assumes the connection and 
// session are already being managed elsewhere.
public class TelemetryReceiverBinder
{
	private IMqttPubSubClient _client;

	public TelemetryReceiverBinder(IMqttPubSubClient client)
	{
		_client = client;
	}
	
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		_client.ApplicationMessageReceivedAsync += handleReceivedMessage;

		MqttClientSubscribeOptions options = new MqttClientSubscribeOptionsBuilder()
			.WithTopicFilter("SomeBinderSpecificTopic")
			.Build();

		await _client.SubscribeAsync(options, cancellationToken);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_client.ApplicationMessageReceivedAsync -= handleReceivedMessage;

		MqttClientUnsubscribeOptions options = new MqttClientUnsubscribeOptionsBuilder()
			.WithTopicFilter("SomeBinderSpecificTopic")
			.Build();

		await _client.UnsubscribeAsync(options, cancellationToken);
	}
}

...

// Note that this layer (the application layer) has access to Connect/Disconnect via 
// the MqttSessionClient implementation of IMqttPubSubClient
private static async Task RunSampleAsync(CancellationToken cancellationToken)
{
	using MqttSessionClient mqttSessionClient = new MqttSessionClient(someSessionClientOptions);

	var telemetryReceiverBinder = new TelemetryReceiverBinder(mqttSessionClient);

	mqttSessionClient.DisconnectedAsync += OnApplicationCrash;

	await mqttSessionClient.ConnectAsync(cancellationToken);

	await telemetryReceiverBinder.StartAsync(cancellationToken);
	await Task.Delay(TimeSpan.FromMinutes(1));

	await telemetryReceiverBinder.StopAsync(cancellationToken);
	await mqttSessionClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
}
```

<details>
<summary>.NET Interface</summary>
<p>

```csharp
public interface IMqttPubSubClient : IAsyncDisposable
{
	/// <summary>
	/// The callback to execute each time a publish is received from the MQTT broker.
	/// </summary>
	/// <remarks>
	/// The MqttApplicationMessageReceivedEventArgs provided in each callback provides
	/// the API for acknoweldging the particular message.
	/// </remarks>
	event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

	/// <summary>
	/// Publish a message to the MQTT broker.
	/// </summary>
	/// <param name="applicationMessage">The message to publish</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the publish.</returns>
	Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);

	/// <summary>
	/// Subscribe to a topic on the MQTT broker.
	/// </summary>
	/// <param name="options">The details of the subscribe.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The MQTT broker's response.</returns>
	Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Unsubscribe from a topic on the MQTT broker.
	/// </summary>
	/// <param name="options">The details of the unsubscribe request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The MQTT broker's response.</returns>
	Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// The session client implementation that adds connect/disconnect and queueing of pubs/subs
/// on top of the existing IMqttPubSubClient APIs. This class should not be required
/// by binders/RPC clients/DSS clients/etc. This class is only for application layer
/// code that controls when to start/stop the MQTT session.
/// </summary>
public class MqttSessionClient : IMqttPubSubClient
{
	/// <summary>
	/// The callback to execute when this client either encounters a fatal error or exhausts the retry policy when attempting
	/// to recover from non-fatal errors.
	/// </summary>
	event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

	/// <summary>
	/// The callback that executes each time a subscribe operation has completed successfully or unsuccessfully.
	/// </summary>
	event Func<ApplicationSubscriptionProcessedEventArgs, Task> ApplicationSubscriptionProcessedAsync;

	/// <summary>
	/// The callback that executes each time an unsubscribe operation has completed successfully or unsuccessfully.
	/// </summary>
	event Func<ApplicationUnsubscriptionProcessedEventArgs, Task> ApplicationUnsubscriptionProcessedAsync;

	/// <summary>
	/// Connect the session client.
	/// </summary>
	/// <remarks>
	/// This client will connect with cleanStart=true for the initial connect and will always connect
	/// with cleanStart=false for any reconnection scenario.
	/// </remarks>
	public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Disconnect the session client and end the MQTT session.
	/// </summary>
	/// <remarks>
	/// This call will delete the session from the broker by passing along a session expiry interval 
	/// of 0 when disconnecting.
	/// </remarks>
	public async Task DisconnectAsync(MqttClientDisconnectOptions? options = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enqueue a publish operation.
	/// </summary>
	/// <param name="applicationMessage">The message that will be published later.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// The returned task will be completed once the message has been enqueued locally, not 
	/// when the message is sent or when the message is acknowledged by the MQTT broker.
	/// </para>
	/// <para>
	/// When the provided message is published, the <see cref="ApplicationMessageProcessedAsync"/> callback will be executed.
	/// </para>
	/// </remarks>
	Task EnqueuePublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enqueue a batch of publish operations.
	/// </summary>
	/// <param name="applicationMessages">The messages that will be published later.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// The returned task will be completed once the messages have been enqueued locally, not 
	/// when the messages are sent or when the messages are acknowledged by the MQTT broker.
	/// </para>
	/// <para>
	/// When the provided message is published, the <see cref="ApplicationMessageProcessedAsync"/> callback will be executed.
	/// </para>
	/// </remarks>
	Task EnqueuePublishesAsync(IEnumerable<MqttApplicationMessage> applicationMessages, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enqueue a subscribe operation.
	/// </summary>
	/// <param name="options">The subscribe operation that will be published later.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// The returned task will be completed once the subscribe operation has been enqueued locally, not 
	/// when the subscription is sent or when the subscription was acknowledged by the MQTT broker.
	/// </para>
	/// <para>
	/// When the provided subscription is published, the <see cref="ApplicationSubscriptionProcessedAsync"/> callback will be executed.
	/// </para>
	/// </remarks>
	Task EnqueueSubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enqueue an unsubscribe operation.
	/// </summary>
	/// <param name="options">The unsubscribe operation that will be published later.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// The returned task will be completed once the unsubscribe operation has been enqueued locally, not 
	/// when the unsubscription is sent or when the unsubscription was acknowledged by the MQTT broker.
	/// </para>
	/// <para>
	/// When the provided unsubscription is published, the <see cref="ApplicationUnsubscriptionProcessedAsync"/> callback will be executed.
	/// </para>
	/// </remarks>
	Task EnqueueUnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default);
}
```

</p>
</details>

### Object Lifespans

Given that a particular application may have more than one binder/RPC/DSS/etc that share a single implementation of the `IMqttPubSubClient` interface, the designed lifespan of each of these objects in relation to each other should follow these rules:

* The interface for the `IMqttPubSubClient` should contain a "Dispose" type method that disconnects and disposes the MQTT client.
* The concrete class for the session client should contain a "Dispose" type method if applicable for the language.
    * When disposing the session client, the underlying MQTT client should be disposed as well.
        * The user will **not** have access to the underlying MQTT client.
* Binders/DSS clients/RPC clients should accept an instance of the `IMqttPubSubClient` and should **not** require the IMqttClient interface from the underlying MQTT library nor the `MqttSessionClient` concrete class.
* Binders/DSS clients/RPC clients cannot and should not try to dispose the `IMqttPubSubClient` instance given to them.
* `MqttSessionClient` instances should only be disposed by the application layer that created them 

In addition, a particular MQTT session client should handle the lifetime of only one MQTT session. If a session client cannot resume the session (i.e., it expected the server to have a session but received a CONNACK with session present false), the session client should notify the user that the session was lost, notify the user about any pending publishes, subscribes and unsubscribes that were not sent, and disconnect from the broker.

<details>
<summary>.NET Binder Example</summary>
<p>

```csharp
public class TelemetrySenderBinder : IDisposable
{
    private IMqttPubSubClient _client;
    private DisposableObject _someDisposableObject;
        
    public TelemetrySenderBinder(IMqttPubSubClient client)
    {
        _client = client;
        _someDisposableObject = new DisposableObject();
    }

	public async Task<Foo> PublishAsync()
	{
		MqttClientPublishResult result = await _client.PublishAsync(...);
		Foo foo = ParsePuback(result);
		return foo;
	}

    public void Dispose()
    {
        // Note that the IMqttPubSubClient cannot and is not supposed to be disposed here
        _someDisposableObject.Dispose();
    }
}
```

</p>
</details>

<details>
<summary>.NET Application Example (user-managed connection)</summary>
<p>

```csharp
IMqttClient userManagedMqttClient = new MqttFactory().CreateMqttClient();

// This callback must contain retry logic to handle connection loss events. 
// This logic has been omitted for simplicity.
userManagedMqttClient.DisconnectedAsync += OnConnectionLost;

var connectionResult = await userManagedMqttClient.ConnectAsync(mqttClientOptions);

// MqttNetPassThroughClient implements the IMqttPubSubClient interface by passing the 
// pub/sub/unsub requests through to the underlying MQTT client. It does not manage the 
// connection for the user.
IMqttPubSubClient pubSubClient = new MqttNetPassThroughClient(userManagedMqttClient);

TelemetrySenderBinder binder = new TelemetrySenderBinder(pubSubClient);

try
{
    await binder.PublishAsync();
}
finally
{
    await userManagedMqttClient.DisconnectAsync();

    // Note that the IMqttPubSubClient instance cannot and should not be disposed here. The user
	// only needs to dispose the underlying MQTT client.    
    userManagedMqttClient.Dispose();
}
```

</p>
</details>

<details>
<summary>.NET Application Example (library-managed connection)</summary>
<p>

```csharp
MqttSessionClient mqttSessionClient = new MqttSessionClient(connectionSettings, sessionClientOptions);
await mqttSessionClient.ConnectAsync();

try
{
    await binder.PublishAsync();
}
finally
{
    await mqttSessionClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), default);
    
    // This call will dispose both the MQTT session client and the underlying MQTT client
    mqttSessionClient.Dispose();
}
```

</p>
</details>

## Session Client Internal Implementation Details

### Request queue (Publish, Subscribe, Unsubscribe)

The proposed MQTT session client includes a request queue for Publish, Subscribe, and Unsubscribe requests. In the case of normal operations, the queuing mechanism allows the session client to process requests in order and to complete delayed acknowledgement. In the case of connection interruption, the queuing mechanism allows the managed client to track incomplete requests and resume processing upon successful retry.

Some MQTT clients (such as MQTTNet) do not expose a queue like this, so the proposed MQTT session client will need to implement and own this queue. Some clients (such as the Go client) do expose the underlying queue, so they may use it instead.

For languages where our session client library implements the queue, the queue must have a configurable max size. When a queue is maxed out and a new publish/subscribe/unsubscribe, the oldest/youngest publish/subscribe/unsubscribe should be discarded depending on user configuration.

### Initial Connection Details

When a user is first connecting their session client, they should be allowed to either connect with CleanStart set to true or false. However, the value of this option will have no bearing on the CleanStart flag value that is used for when reconnecting.

### Reconnection Details

When a session client has lost the connection and tries to reconnect to the MQTT broker, it must use CleanStart=false to try to recover the session.

If the MQTT broker accepts the connection, but the CONNACK includes isSessionPresent=false, then the session client must close the connection and notify the application layer that the session was lost. This particular scenario is considered a "catastrophic error" wherein the user must be made aware that the session was lost because of potential message loss.

The session client should not attempt to fake recovering the session by sending SUBSCRIBE packets to the broker to recover subscriptions lost when the session was lost.

### Disconnection Details

When a user disconnects the session client, the session client must ensure that a session expiry interval of 0 is sent along with this DISCONNECT packet. This ensures that the broker expires the session immediately after the disconnection.

### Pub Ack Handling

#### Ways to acknowledge

The proposed session client will allow users to manually acknowledge a received publish at any time and from any thread. If the language's MQTT library provides it, the proposed client should also allow users to auto-acknowledge messages. Note that auto-acknowledged messages still need to be enqueued in the correct order just like any other acknowledgement.

Some MQTT client libraries (such as the Go Paho client) already provide this feature. Forlibraries that don't, queueing logic will need to exist in the session client layer.

#### Ack Ordering

The proposed session client will deliver publish acknowledgements in the order the publishes were received in. The order that the user acknowledges them is disregarded.

In order to provide this guarantee, the proposed session client may only acknowledge a publish if every publish received before it has already been acknowledged. For example, if the client receives publishes 1 and 2, and the user only acknowledges publish 2, neither publish 1 or 2 will actually be acknowledged on the MQTT connection until the user also acknowledges publish 1.

In cases where the user callback for a message never completes, the session client will never send an acknowledgement for the message and will block subsequent messages from being received.

In cases where the user callback for a message throws, the session client will send the acknowledgement as to not block subsequent messages from being acknowledged.

#### Session Cleanup

The session client must clear the queue of ACKs if a disconnection event occurs. Any queued acknowledgements that were not sent prior to the disconnect should be abandoned. The expected behavior from a user of this client should be that un-acknowledged messages are re-delivered.

Note that this design means that, if a client receives message1, then message2, the user acks message2, then a disconnect happens, the session client will not send an acknowledgement for
either message. In this case, the client will receive message1 and message2 again. While it is undesirable for the user to see a message again that they believe they have acked already,
QoS 1 behavior provides cover here.

#### Acknowledgement API design

When the MQTT session client notifies the application layer that a message was received, the provided message object should contain both a settable flag for opting out of automatic acknowledgement as well as a function for acknowledging the message.

Importantly, the default behavior of the session client should be to automatically acknowledge a message so that any unhandled messages that go un-acked do not block the PUBACK queue.

Due to this design, the session client should be blocking on these callbacks finishing so that the application layer has the opportunity to set the `AutoAcknowledge` flag before the session client attempts to automatically acknowledge the message.

<details>
<summary>.NET Acknowledgement API Example</summary>
<p>

```csharp
MqttSessionClient mqttSessionClient = new MqttSessionClient(connectionSettings, sessionClientOptions);

mqttSessionClient.ApplicationMessageReceivedAsync += OnMessageReceived;
await mqttSessionClient.ConnectAsync();

try
{
    await mqttSessionClient.SubscribeAsync("someTopic");
	await Task.Delay(...);
}
finally
{
    await mqttSessionClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), default);
    
    // This call will dispose both the MQTT session client and the underlying MQTT client
    mqttSessionClient.Dispose();
}

private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
	// Users can signal that this message will be manually ack'd instead
	args.AutoAcknowledge = false;

	// Each message can be acknowledged manually even from other threads
	new Task(() => 
	{
		await Task.Delay(...);
		await args.AcknowledgeAsync();
	}).Start();
}
```
  
</p>
</details>

### Session Management - Connection Settings and Retry Policy

The overall principle of session management is either for the application to own the connection and session by passing in an MQTT client to their binders or for the MQTT session client to create the MQTT client and own the connection and session. There is no case of mixed ownership of the connection. 

In the case where the MQTT client is provided, it is the provider’s responsibility to manage all aspects of connection and session. No session client will be constructed in this case.

In the case where the application wants a managed connection, the MQTT session client simplifies the connection setup and connection maintenance process by handling retriable connection interruptions based on user-configured timeout and retry policy specifications. For fatal connection or session failures, the MQTT session client will notify the user application.

We will use a generic connection settings structure to capture MQTT connection specific parameters. The application can use this settings structure to create the MQTT connection or provide the settings to the proposed managed client as a consistent approach to create a connection.

<details>
<summary>.NET - Client settings & objects</summary>
<p>

```csharp
public class MqttSessionClient : IMqttPubSubClient
{
	/// <summary>
	/// Create a MQTT session client. This client maintains the connection for you.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This client will automatically recover the connection and all previous subscriptions if it
	/// detects that the previous connection was lost. It will also enqueue
	/// publishes/subscribes/unsubscribes and send them when the connection is alive. This client will
	/// use a false cleanStart flag to recover subscriptions when reconnecting.
	/// </para>
	/// <para>
	/// An MQTT session client will only report connection loss and/or publish/subscribe/unsubscribe
	/// failures if they are deemed fatal or if the provided retry policy is exhausted. All transient failures will cause the
	/// retry policy to be checked, but won't cause the <see cref="DisconnectedAsync"/> event to fire.
	/// </para>
	/// </remarks>
	/// <param name="sessionClientOptions">The configurable options for this MQTT session client.</param>
	/// <param names="connectionSettings">The configurable options for each connection that the
	/// session client will create and maintain.</param>
	public MqttSessionClient(MqttSessionClientOptions sessionClientOptions, MqttConnectionSettings connectionSettings);
}

public class MqttSessionClientOptions
{	
	public uint MaxPendingMessages { get; set; } = uint.MaxValue;
	
	public MqttPendingMessagesOverflowStrategy PendingMessagesOverflowStrategy { get; set; } = MqttPendingMessagesOverflowStrategy.DropNewMessage;
	
	public IRetryPolicy ConnectionRetryPolicy { get; set; } = new ExponentialBackoffRetryPolicy(uint.MaxValue, TimeSpan.MaxValue);
	
	public IRetryPolicy OperationRetryPolicy { get; set; } = new ExponentialBackoffRetryPolicy(uint.MaxValue, TimeSpan.MaxValue);
	
	public ILogger? Logger { get; set; }
	
	public TimeSpan CanceledMessageCheckPeriod { get; set; } = TimeSpan.FromSeconds(5);
}

public enum MqttPendingMessagesOverflowStrategy
{
    DropOldestQueuedMessage,
    DropNewMessage
}
```

</p>
</details>

<details>
<summary>.NET - Connection Retry Policy Interface</summary>
<p>
  
```csharp
public interface IRetryPolicy
{
	/// <summary>
	/// Method called by the client when an operation fails to determine if a retry should be attempted,
	/// and how long to wait until retrying the operation.
	/// </summary>
	/// <param name="currentRetryCount">The number of times the current operation has been attempted.</param>
	/// <param name="lastException">The exception that prompted this retry policy check.</param>
	/// <param name="retryDelay">Set this to the desired time to delay before the next attempt.</param>
	/// <returns>True if the operation should be retried; otherwise false.</returns>
	/// <example>
	/// <code language="csharp">
	/// class CustomRetryPolicy : IRetryPolicy
	/// {
	///     public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay)
	///     {
	///         // Add custom logic as needed upon determining if it should retry and set the retryDelay out parameter
	///     }
	/// }
	/// </code>
	/// </example>
	bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
}

// The default retry policy, but users can create their own custom retry policy.
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
	/// <summary>
	/// Creates an instance of this class.
	/// </summary>
	/// <param name="maxRetries">The maximum number of retry attempts; use 0 for infinite retries.</param>
	/// <param name="maxWait">The maximum amount of time to wait between retries (will not exceed ~12.43 days).</param>
	/// <param name="useJitter">Whether to add a small, random adjustment to the retry delay to avoid synchronicity in clients retrying.</param>
	/// <example>
	/// <code language="csharp">
	/// var exponentialBackoffRetryPolicy = new IotHubClientExponentialBackoffRetryPolicy(maxRetries: 10, maxWait: TimeSpan.FromSeconds(30), useJitter: true);
	/// 
	/// var clientOptions = new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.WebSocket))
	/// {
	///     RetryPolicy = exponentialBackoffRetryPolicy,
	/// };
	/// </code>
	/// </example>
	public ExponentialBackoffRetryPolicy(uint maxRetries, TimeSpan maxWait, bool useJitter = true);

	/// <inheritdoc/>
	public bool ShouldRetry(uint currentRetryCount, Exception lastException, out TimeSpan retryDelay);
}
```

</p>
</details>

<details>
<summary>Go - Client settings & objects</summary>
<p>
  
```go
type DefaultClientConnectionSettings struct {
	ServerURL                 *url.URL
	TLSConfig                 *tls.Config
	MQTTClientID              string
	MQTTKeepAlive             uint16
	MQTTSessionExpiryInterval uint32
	MQTTUserName              string
	MQTTPassword              []byte
	MQTTReceiveMaximum        uint16
}

type DefaultClientOptions struct {
	ConnectionRetryStrategy     RetryOperationFactory // see "Go - Connection Retry Policy Interface" flyout below
	ConnectionSettings          *DefaultClientConnectionSettings
	PublishQueue                queue.Queue // provided by autopaho (https://github.com/eclipse/paho.golang/blob/master/autopaho/queue/queue.go)
	Session                     session.SessionManager // provided by paho (https://github.com/eclipse/paho.golang/blob/master/paho/session/session.go)
	CleanStartOnFirstConnection bool
	LogHandler                  slog.Handler
}

type DefaultClient struct { /* internal (unexported) fields */ }

// Create a new DefaultClient instance with the given options
func NewDefaultClient(options *DefaultClientOptions) (*DefaultClient, error)

// Connect the client and start monitoring connection to keep the session alive
func (c *DefaultClient) StartSession(ctx context.Context) error

// DefaultClient methods to implement MQTTClient interface (defined in "Core async interfaces" section)
```

</p>
</details>

<details>
<summary>Go - Connection Retry Policy Interface</summary>
<p>

```go
type RetriesExhaustedError struct{}

func (e *RetriesExhaustedError) Error() string {
	return "retries exhausted"
}

type RetryOperation interface {
	// WaitForRetry blocks until the next retry should be attempted, or the context is canceled.
	// Returns an error if the context is canceled or the retries have been exhausted (in which case the error is of type *RetriesExhaustedError).
	WaitForRetry(ctx context.Context) error
}

type RetryOperationFactory interface {
	NewRetryOperation() RetryOperation
}

// Implements RetryOperationFactory
type ExponentialBackoffFactory struct {
	/* internal (unexported) fields */
}

// Implements RetryOperation
type ExponentialBackoff struct {
	/* internal (unexported) fields */
}

type ExponentalBackoffFactoryOptions {
	MinDelayMs float64
	MaxDelayMs float64
	Multiplier float64
	JitterMs   float64
	MaxRetries int
}

// Creates an ExponentialBackoffFactory with the given options
func NewExponentialBackoffFactory(options *ExponentialBackoffFactoryOptions) *ExponentialBackoffFactory

// ExponentialBackoffFactory implementation of RetryOperationFactory. Returns an ExponentialBackoff.
func (f *ExponentialBackoffFactory) NewRetryOperation() RetryOperation
```

</p>
</details>

<details>
<summary>Rust - Client settings & structs</summary>
<p>

```rust
pub struct MqttConnectionSettings {
    client_id: String,
    host_name: String,
    tcp_port: u16,
    keep_alive: Duration,
    session_expiry: Duration,
    connection_timeout: Duration,
    clean_start: bool,
    username: Option<String>,
    password: Option<String>,
    password_file: Option<String>,
    use_tls: bool,
    ca_file: Option<String>,
    ca_require_revocation_check: bool,
    cert_file: Option<String>,
    key_file: Option<String>,
    key_file_password: Option<String>,
    sat_auth_file: Option<String>,
}

pub struct SessionOptions {
    pub connection_settings: MqttConnectionSettings,
    pub reconnect_policy: Box<dyn ReconnectPolicy>,
}

pub trait ReconnectPolicy {
    fn next_reconnect_delay(&self, prev_attempts: u32, error: &ConnectionError)
        -> Option<Duration>;
}

```
</p>
</details>

#### What is retried and what is not

When a user creates an MQTT session client to handles the session management for them, all connection and session level failures (such as connection loss) will be handled by this MQTT session client. All application level failures (such as a publish packet being acknowledged with a non-success reason code) will simply be returned to the user via the respective publish/subscribe/unsubscribe API. For context on why this decision was made, see the appendix.

Connection and Session level failures (Retried):

* Connection is lost due to keepalive timeout
    * The MQTT session client is expected to send a new CONNECT packet with cleanSession = false to reestablish the session.
* Subscribe/Unsubscribe request is made, but a connection loss happens before any SUBACK/UNSUBACK is received.
    * The MQTT session client is expected to requeue the subscribes and unsubscribes that weren't acknowledged yet and send them again when the connection is re-established.
* Publish request is made, but a connection loss happens before any PUBACK is received.*
    * The MQTT session client is expected to requeue the publishes that weren't acknowledged yet and send them again when the connection is reestablished.
    * This case may already be covered by the underlying MQTT client. If it is, then the session client does not need to resend the unacknowledged messages. 
* Connection is lost due to the session being taken over
    * The MQTT session client is expected to report this as a fatal error to the user and not attempt to recover the session

Application level failures (Not retried):

* Publish packet acknowledged with non-success code (0x00).
* Subscribe packet acknowledged with non-success code (0x00, 0x01 or 0x02 depending on requested QoS).
    * Note that this does mean user applications will need to check that each subscription's QoS granted matches what they requested.

Miscellaneous cases:

* Session client reconnects (with clean session = false as it always should), but the server sends sessionPresent = false in the CONNACK
    * The session client should throw a fatal error up to the user to notify them that the session has ended. Session loss may result in message loss since the previous session may have had enqueued messages on the broker side that won't be sent to the client now that the session is lost.
* Session client gets disconnected by the server with a DISCONNECT packet
    * The session client should NOT try to reconnect if the reason code is 0x8E Session Taken Over.
    * In all other cases, the session client should reconnect if the reason code is transient (e.g., 0x97 Quota Exceeded) and should not reconnect if the reason code is permanant (e.g., 0x9D Server Moved)

## Appendix

### Should we include "Enqueue" type APIs in the IMqttPubSubClient interface?

We are opting **not** to include APIs such as "EnqueuePublishAsync(...)" in the IMqttPubSubClient for 3 reasons:

1. Simplicity of the interface

	Adding these enqueue style APIs means we also need to define callbacks to invoke when each enqueued operation finishes. This could also lead to some confusion by users who think they need to listen to these callbacks even if they use the non-queueing style APIs.

2. Incompatibility with pass-through implementations of this interface

    MQTT clients don't all provide the ability to enqueue messages even though the MQTT spec suggests it should exist. MQTTNet, for instance, does not contain a message queue so there is no way to publish a message but return only when the message is enqueued. 

3. Lack of demand

    None of DSS, RPC, or the other primitive binders we have so far require complex queueing of messages.

### Should we use primitive types from the underlying MQTT client?

Neither the `MqttSessionClient` nor the `IMqttPubSubClient` interface should expose any interfaces or concrete classes defined in the underlying MQTT client library. This both protects us from breaking changes from the underlying library and gives users the ability to use other MQTT clients as well.

Underlying MQTT client library types are acceptable in the basic "pass-through" implementations of
the `IMqttPubSubClient` interface, though. For example:

```csharp
public class MqttNetPassThroughClient : IMqttPubSubClient
{
	...

	// This connect operation is allowed to use the MqttClientconnectResult class directly from
	// MQTTNet since this client is the MQTTNet pass-through implementation of IMqttPubSubClient.
	public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default);

	...
}
```

### What is missing from existing MQTT clients

.NET - MQTTNet Managed Client exists, but with limitations

* Retry policy isn’t configurable.
* No checking for fatal errors, will continue to retry forever.
* Doesn’t fulfil the same interface as the base MQTT client. This makes it difficult for RPC/DSS/binders/etc. to interchangeably accept user-managed MQTT client and SDK-managed MQTT client.
* Doesn't notify user if a session (note session, not connection) was lost. Tries to hide it by re-subscribing to previous session's topics. This could result in message loss.

Go - [Autopaho](https://github.com/eclipse/paho.golang/tree/master/autopaho) Managed Client exists in beta, but with limitations. The items in **bold** must be addressed in the [base Paho client](https://github.com/eclipse/paho.golang/tree/master/paho) if we want to address the issues in a higher-level managed client, even if we own the managed client implementation.

* [Lack of manual acking support](https://github.com/eclipse/paho.golang/issues/160)
* [Reauthentication with enhanced auth is not supported](https://github.com/eclipse/paho.golang/issues/212)
* Doesn’t fulfill the same interface as the base MQTT client, makes it difficult to interchangeably accept user-managed client and SDK-managed client
* No mechanism for retrying failed MQTT operations within a single network connection (i.e., if a PUBLISH fails due to a reason code we deem retryable)
* Reconnection timeouts and changes in connection settings (e.g., TLS configuration) configurable in a convoluted way by setting a custom AttemptConnection function in the config
* Cannot use reason code of server DISCONNECT packet to determine retry strategy
* **[Base client does not have a way for the application to get back the PUBACK on an async publish](https://github.com/eclipse/paho.golang/issues/207#issuecomment-1872266599), which means the managed client cannot get return the PUBACK.**

Rust – Rumqttc MQTT Client
- TODO

Java – Paho Java MQTT Client - Base MQTT client contains basic “auto-reconnect" flag, but... 	
* No configurable retry policy  
* No distinction between fatal vs recoverable errors.

Python – Paho Python MQTT Client
* No configurable reconnect policy (will simply just try to reconnect indefinitely, or not reconnect at all) 
* Lacks manual ACK support (automatically acks on QoS 1 upon incoming message callback return)
* Lacks support for multiple callbacks on the same trigger – this can be hacked around by defining a callback that calls other callbacks, but this becomes an issue when using clients provided by a user, or providing clients to a user, as this workaround lacks portability 
* Not natively async/await; needs a wrapper to support this. Furthermore, async/await paradigm can clash with the existing callback/thread-based asynchronicity paradigm. We can largely get away with whatever we need to do internally to make this work, but becomes an issue when trying to give/receive clients to/from users. 
* Edge case where connection failing at a specific time can potentially cause code to hang waiting on message delivery, even after connection is re-established and message is delivered (known bug) 
* Allows configuration of max size of outgoing message queue, but no direct access via interface

Conclusion: Some implementations of the MQTT libraries have a managed client with significant limitations for the SDK use cases. 
To support our the MQTT broker features with a managed client, we need a consistent API feature set in a managed client 
to handle session management. During this initial development phase, we will design a managed client and its interfaces independent of the native managed 
client, should there be one in some of the MQTT libraries. Once we have a working implementation of a managed client, it will be a separate 
discussion with MQTTnet and Eclipse Paho Projects to contribute the resulting feature sets or complete client to their corresponding project repo.

### Connection Object Creation – Factory Methods

In some earlier design discussions, we considered a design where an MQTT client factory would be passed around to the various binders/RPC clients/DSS clients/etc, and each MQTT operation would request a connected + subscribed MQTT client from the factory. After testing out proof-of-concept implementations for this design, we ran into some challenges with this approach.

The fundamental problem with this design is that in order to consistently provide an MQTT client with the desired session state, the factory itself must track the desired state of the user. That means the factory maintains a queue of publish/subscribe/unsubscribe requests, checks connection state, and handles transient disconnections. This kind of stateful-ness is atypical for a factory and more befitting of a client object.

Another problem is that there is no way to prevent a user from caching the first client returned by factory.GetClient() call and re-using that client. If they did that, then there would be no guarantees that the client will stay in the desired state. Given that factory methods are usually used this way, asking users to call factory.GetClient() repeatedly would be unintuitive.

With the above considered, we are opting to instead pass around a single resilient managed MQTT client to the various binders/RPC clients/DSS clients/etc. in a user’s application.

That said, if factories are the common pattern for the language, a factory pattern may be used to create the initial managed client instance that gets passed around to the various binders/RPC clients/DSS clients/etc. The factory itself should not be passed around, though.

### Retrying MQTT operations that complete with an error reason code

In some cases, an application may want to retry an MQTT operation that completes with an error reason code. For example, if a Publish operation completes with a PUBACK reason code indicating "Quota Exceeded", the application may want to try to send the publish again, after a delay. We discussed the possibility of adding that functionality into our managed client. However, there is a major pitfall to this approach that makes this infeasible. If a reconnection occurs after a publish request gets moved from the queue and into the MQTT session, getting the reason code for that publish would be difficult if not impossible. In this case, the we would have to check the status of and potentially retry any publish that gets redelivered when re-establishing a connection on an existing session.

From the MQTT perspective, a Publish/Subscribe/Unsubscribe operation is completed once the ACKing flow is completed. Retries beyond this are at the application level rather than the protocol level.

### Go and Rust design so far

Note that both the rust and Go interface designs below are still under construction and may not match the .NET design at the moment.

<details>
<summary>Go Interface</summary>
<p>

```go
type IncomingPublish struct {
	PublishPacket  *paho.Publish
	AlreadyHandled bool
	Errs           []error
	AckFunc        func(context.Context) error
}

// PublishHandler is a user-defined function that handles incoming publish messages.
// If the handler returns true, the message is considered handled and the subsequent receive AlreadyHandler=true
// If the handler returns an error, the error is added to the slice of errors and the subsequent receive the slice of errors.
// The AckFunc function in IncomingPublish sends an ack to the server if applicable.
// If manual acks are unavailable on the client, AckFunc will return a *ManualAcksUnavailableError.
// If the QoS of the publish is 0, AckFunc will return a *ManualAcksQoSError.
// If the connection was lost since the publish was received, AckFunc will return a *ManualAcksConnectionLostError.
// If the publish has already been acked, AckFunc will return a *ManualAcksAlreadyAckedError.
type PublishHandler func(incoming *IncomingPublish) (bool, error)

type SubscribeOptions struct {
	// ResultCallback is called when the subscribe operation completes.
	// If the client implementation is unable to determine the result of the operation,
	// the error will be of type *ResponseUnavailableError.
	ResultCallback func(*paho.Suback, error)
}

type UnsubscribeOptions struct {
	// ResultCallback is called when the unsubscribe operation completes.
	// If the client implementation is unable to determine the result of the operation,
	// the error will be of type *ResponseUnavailableError.
	ResultCallback func(*paho.Unsuback, error)
}

type PublishOptions struct {
	// ResultCallback is called when the publish operation completes.
	// If the client implementation is unable to determine the result of the operation,
	// the error will be of type *ResponseUnavailableError.
	ResultCallback func(*paho.PublishResponse, error)
}

type MQTTClient interface {
	// RegisterOnFatalErrorHandler registers a handler to be notified of fatal errors.
	// Handlers are added to a list and are executed asynchronously.
	// If the client is already in a fatal error state, the handler will be called immediately.
	// The function returned by RegisterOnFatalErrorHandler removes the handler from the list when called.
	// Some implementations may not support fatal error handlers, in which case the returned function will be nil.
	RegisterOnFatalErrorHandler(func(error)) func()

	// RegisterPublishHandler registers a handler for incoming publish messages.
	// Handlers are added to a list and are executed synchronously in the order they were added, so it is important that handlers do not block.
	// The function returned by RegisterPublishHandler removes the handler from the list when called.
	RegisterPublishHandler(PublishHandler) func()

	// Subscribe asychronously executes a subscribe operation.
	// The result of the operation is returned via the ResultCallback in the options.
	Subscribe(context.Context, *paho.Subscribe, *SubscribeOptions) error

	// Unsubscribe asychronously executes an unsubscribe operation.
	// The result of the operation is returned via the ResultCallback in the options.
	Unsubscribe(context.Context, *paho.Unsubscribe, *UnsubscribeOptions) error

	// Publish asychronously executes a publish operation.
	// The result of the operation is returned via the ResultCallback in the options.
	Publish(context.Context, *paho.Publish, *PublishOptions) error
}
```

</p>
</details>

<details>
<summary>Rust Interface</summary>
<p>

```rust
pub trait MqttProvider<PS, PR>
where
    PS: MqttPubSub + Clone + Send + Sync,
    PR: MqttPubReceiver + Send + Sync,
{
	fn client_id(&self) -> &str;
	fn pub_sub(&self) -> PS;
	fn filtered_pub_receiver(
			&mut self,
			topic_filter: &str,
			auto_ack: bool,
	) -> Result<PR, TopicParseError>;
}
pub trait MqttPubSub {
	async fn publish(
        &self,
        topic: impl Into<String> + Send,
        qos: QoS,
        retain: bool,
        payload: impl Into<Bytes> + Send,
    ) -> Result<CompletionToken, MqttClientError>;
	async fn publish_with_properties(
			&self,
			topic: impl Into<String> + Send,
			qos: QoS,
			retain: bool,
			payload: impl Into<Bytes> + Send,
			properties: PublishProperties,
	) -> Result<CompletionToken, MqttClientError>;
	async fn subscribe(
			&self,
			topic: impl Into<String> + Send,
			qos: QoS,
	) -> Result<CompletionToken, MqttClientError>;
	async fn subscribe_with_properties(
			&self,
			topic: impl Into<String> + Send,
			qos: QoS,
			properties: SubscribeProperties,
	) -> Result<CompletionToken, MqttClientError>;
	async fn unsubscribe(
			&self,
			topic: impl Into<String> + Send,
	) -> Result<CompletionToken, MqttClientError>;
	async fn unsubscribe_with_properties(
			&self,
			topic: impl Into<String> + Send,
			properties: UnsubscribeProperties,
	) -> Result<CompletionToken, MqttClientError>;
}
pub trait MqttAck {
	async fn ack(&self, publish: &Publish) -> Result<(), MqttClientError>;
}
pub trait MqttDisconnect {
	async fn disconnect(&self) -> Result<(), MqttClientError>;
}
pub trait MqttEventLoop {
	async fn poll(&mut self) -> Result<Event, ConnectionError>;
}
pub trait MqttPubReceiver {
	async fn recv(&mut self) -> Option<Publish>;
}
```
</p>
</details>

