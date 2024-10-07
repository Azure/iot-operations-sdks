namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.STK
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Diagnostics;
    using MQTTnet.Formatter;
    using MQTTnet.Internal;
    using MQTTnet.Packets;

    public class EmulatedClient : IMqttClient
    {
        private readonly AsyncEvent<MqttApplicationMessageReceivedEventArgs> _applicationMessageReceivedEvent = new AsyncEvent<MqttApplicationMessageReceivedEventArgs>();

        private readonly MqttEmulator emulator;
        private readonly List<Func<MqttApplicationMessageReceivedEventArgs, Task>> handlers;

        private ushort packetIdSequencer;

        public bool IsConnected => true;

        public MqttClientOptions Options { get; }

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync
        {
            add => handlers.Add(value);
            remove => throw new NotImplementedException();
        }

#pragma warning disable CS0414 // The field is assigned but its value is never used
        public event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync = default!;
        public event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync = default!;
        public event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync = default!;
        public event Func<InspectMqttPacketEventArgs, Task> InspectPackage = default!;
        public event Func<InspectMqttPacketEventArgs, Task> InspectPacketAsync = default!;
#pragma warning restore CS0414

        public EmulatedClient(string clientId, MqttEmulator emulator)
        {
            this.emulator = emulator;
            handlers = new();
            packetIdSequencer = 0;
            Options = new() { ClientId = clientId, ProtocolVersion = MqttProtocolVersion.V500 };
        }

        public Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public Task PingAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
        {
            return emulator.PublishAsync(applicationMessage, cancellationToken);
        }

        public Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
        {
            emulator.Subscribe(Options.ClientId, options.TopicFilters);
            return Task.FromResult(new MqttClientSubscribeResult(1, new List<MqttClientSubscribeResultItem>(), "mock", new List<MqttUserProperty>()));
        }

        public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task ReceiveAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
        {
            CountdownEvent ackEvent = new(1);
            var acker = (MqttApplicationMessageReceivedEventArgs _, CancellationToken _) => { ackEvent.Signal(); return Task.CompletedTask; };

            unchecked { ++packetIdSequencer; };
            var eventArgs = new MqttApplicationMessageReceivedEventArgs(Options.ClientId, applicationMessage, new MqttPublishPacket { PacketIdentifier = packetIdSequencer }, acker);

            foreach (Func<MqttApplicationMessageReceivedEventArgs, Task> handler in handlers)
            {
                await handler(eventArgs);
            }

            if (!eventArgs.AutoAcknowledge)
            {
                await Task.Run(() => { ackEvent.Wait(); });
            }
        }
    }
}
