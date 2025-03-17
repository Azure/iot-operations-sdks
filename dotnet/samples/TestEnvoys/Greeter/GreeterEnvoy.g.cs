#nullable enable

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

namespace TestEnvoys.Greeter;

public class GreeterEnvoy
{

    public class HelloRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class HelloWithDelayRequest : HelloRequest
    {
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    }

    public class HelloResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    [CommandTopic("rpc/samples/hello")]
    public class SayHelloCommandExecutor : CommandExecutor<HelloRequest, HelloResponse>
    {
        public SayHelloCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient, "sayHello", new Utf8JsonSerializer())
        {
        }
    }

    [CommandTopic("rpc/samples/hello/delay")]
    public class SayHelloWithDelayCommandExecutor : CommandExecutor<HelloWithDelayRequest, HelloResponse>
    {
        public SayHelloWithDelayCommandExecutor(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient, "sayHelloWithDelay", new Utf8JsonSerializer())
        {
            IsIdempotent = true;
            CacheTtl = TimeSpan.FromSeconds(10);
            ExecutionTimeout = TimeSpan.FromSeconds(30);
        }
    }

    public abstract class Service : IAsyncDisposable
    {
        readonly SayHelloCommandExecutor sayHelloExecutor;
        readonly SayHelloWithDelayCommandExecutor sayHelloWithDelayExecutor;
        public Service(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        {
            sayHelloExecutor = new SayHelloCommandExecutor(applicationContext, mqttClient)
            {
                OnCommandReceived = SayHello,
            };

            sayHelloWithDelayExecutor = new SayHelloWithDelayCommandExecutor(applicationContext, mqttClient)
            {
                OnCommandReceived = SayHelloWithDelayAsync,
            };
        }

        public SayHelloCommandExecutor SayHelloCommandExecutor { get => sayHelloExecutor; }
        public SayHelloWithDelayCommandExecutor SayHelloWithDelayCommandExecutor { get => sayHelloWithDelayExecutor; }

        public async ValueTask DisposeAsync()
        {
            await SayHelloCommandExecutor.DisposeAsync();
            await SayHelloWithDelayCommandExecutor.DisposeAsync();
        }

        public async ValueTask DisposeAsync(bool disposing)
        {
            await SayHelloCommandExecutor.DisposeAsync(disposing);
            await SayHelloWithDelayCommandExecutor.DisposeAsync(disposing);
        }

        public abstract Task<ExtendedResponse<HelloResponse>> SayHello(ExtendedRequest<HelloRequest> request, CancellationToken cancellationToken);
        public abstract Task<ExtendedResponse<HelloResponse>> SayHelloWithDelayAsync(ExtendedRequest<HelloWithDelayRequest> request, CancellationToken cancellationToken);

        public async Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
        {
            await sayHelloExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken);
            await sayHelloWithDelayExecutor.StartAsync(preferredDispatchConcurrency, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        public override string ToString()
        {
            return $"Service {nameof(GreeterEnvoy)} {Environment.NewLine}" +
                    $"\tSayHello: Idempotent {sayHelloExecutor.IsIdempotent} CacheDuration {sayHelloExecutor.CacheTtl} {Environment.NewLine}" +
                    $"\tSayHelloWithDelay: Idempotent {sayHelloWithDelayExecutor.IsIdempotent} CacheDuration {sayHelloWithDelayExecutor.CacheTtl} {Environment.NewLine}";

        }
    }

    [CommandTopic("rpc/samples/hello")]
    public class SayHelloCommandInvoker : CommandInvoker<HelloRequest, HelloResponse>
    {
        public SayHelloCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient, "sayHello", new Utf8JsonSerializer())
        {
            ResponseTopicPrefix = "clients/{invokerClientId}";
        }
    }

    [CommandTopic("rpc/samples/hello/delay")]
    public class SayHelloWithDelayCommandInvoker : CommandInvoker<HelloWithDelayRequest, HelloResponse>
    {
        public SayHelloWithDelayCommandInvoker(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
            : base(applicationContext, mqttClient, "sayHelloWithDelay", new Utf8JsonSerializer())
        {
            ResponseTopicPrefix = "clients/{invokerClientId}";
        }
    }

    public class Client : IAsyncDisposable
    {
        private IMqttPubSubClient mqttClient;
        readonly SayHelloCommandInvoker sayHelloInvoker;
        readonly SayHelloWithDelayCommandInvoker sayHelloWithDelayInvoker;

        public Client(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
        {
            this.mqttClient = mqttClient;
            sayHelloInvoker = new SayHelloCommandInvoker(applicationContext, mqttClient);
            sayHelloWithDelayInvoker = new SayHelloWithDelayCommandInvoker(applicationContext, mqttClient);
        }

        public SayHelloCommandInvoker SayHelloCommandInvoker { get => sayHelloInvoker; }
        public SayHelloWithDelayCommandInvoker SayHelloWithDelayCommandInvoker { get => sayHelloWithDelayInvoker; }

        public RpcCallAsync<HelloResponse> SayHello(ExtendedRequest<HelloRequest> request, CommandRequestMetadata? md = default, TimeSpan? timeout = default)
        {
            string? clientId = this.mqttClient.ClientId;
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
            }

            CommandRequestMetadata metadata = md == default ? new CommandRequestMetadata() : md;
            Dictionary<string, string> transientTopicTokenMap = new() { { "invokerClientId", clientId } };
            return new RpcCallAsync<HelloResponse>(sayHelloInvoker.InvokeCommandAsync(request.Request, metadata, transientTopicTokenMap, timeout), metadata.CorrelationId);
        }

        public RpcCallAsync<HelloResponse> SayHelloWithDelay(ExtendedRequest<HelloWithDelayRequest> request, TimeSpan? timeout = default)
        {
            string? clientId = this.mqttClient.ClientId;
            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("No MQTT client Id configured. Must connect to MQTT broker before invoking command.");
            }

            CommandRequestMetadata metadata = new CommandRequestMetadata();
            Dictionary<string, string> transientTopicTokenMap = new() { { "invokerClientId", clientId } };
            return new RpcCallAsync<HelloResponse>(sayHelloWithDelayInvoker.InvokeCommandAsync(request.Request, metadata, transientTopicTokenMap, timeout), metadata.CorrelationId);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await sayHelloInvoker.DisposeAsync();
            await sayHelloWithDelayInvoker.DisposeAsync();
        }
    }
}
