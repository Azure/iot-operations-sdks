using Azure.Iot.Operations.FaultableMqttBroker;
using MQTTnet;
using MQTTnet.Diagnostics;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Azure.Iot.Operations.Protocol.Tools
{
    public class FaultableMqttBroker
    {
        private MqttServer _mqttServer;
        private const string disconnectFaultName = "fault:disconnect";
        private const string faultDelayName = "fault:delay";
        private const string rejectConnectFaultName = "fault:rejectconnect";
        private const string rejectPublishFaultName = "fault:rejectpublish";
        private const string rejectSubscribeFaultName = "fault:rejectsubscribe";
        private const string rejectUnsubscribeFaultName = "fault:rejectunsubscribe";
        private const string faultRequestIdName = "fault:requestid";

        // Fault injection requests should come with a user property with key "fault:requestid" and a unique value.
        // By tracking which requests this broker has already received, the broker knows not to inject
        // the same fault again. For instance, a typical fault injection test may look something like this:
        //
        // Open a connection to this broker and send a single publish that should be interrupted by a disconnect.
        // We want to test that the client sends the same message again after recovering from the fault, but
        // we don't want the broker to cause a fault again.
        //
        // Note that these values are never removed from the broker. Right now, this broker is very
        // short-lived, so it doesn't matter if we clear these entries or not.
        private List<string> PublishRequestsAlreadyReceived = new();
        private List<string> SubscribeRequestsAlreadyReceived = new();
        private List<string> UnsubscribeRequestsAlreadyReceived = new();
        private List<string> ConnectRequestsAlreadyReceived = new();

        public FaultableMqttBroker(int port)
        {
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(port)
                .WithPersistentSessions(true)
                .Build();

            _mqttServer = new MqttFactory(new ConsoleLogger()).CreateMqttServer(options);

            _mqttServer.InterceptingPublishAsync += async (args) =>
            {
                bool requestIdPresent = TryGetUserProperty(args.ApplicationMessage.UserProperties, faultRequestIdName, out string? requestIdValue);
                if (!requestIdPresent || !PublishRequestsAlreadyReceived.Contains(requestIdValue!))
                {
                    if (requestIdValue != null)
                    {
                        PublishRequestsAlreadyReceived.Add(requestIdValue);
                    }

                    FaultHandling faultHandling = await CloseConnectionIfRequested(args.ApplicationMessage.UserProperties, args.ClientId);
                    if (faultHandling.FaultHandled)
                    {
                        args.ProcessPublish = faultHandling.SendAck;
                        return;
                    }
                    else if (TryGetUserProperty(args.ApplicationMessage.UserProperties, rejectPublishFaultName, out string? rejectPublishReasonString))
                    {
                        if (int.TryParse(rejectPublishReasonString, out int pubackCodeInt))
                        {
                            try
                            {
                                args.Response.ReasonCode = (MqttPubAckReasonCode)pubackCodeInt;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Failed to convert the provided puback code int into a a valid puback code enum value. No fault will be triggered.");
                                return;
                            }
                        }
                    }
                }
            };

            _mqttServer.InterceptingSubscriptionAsync += async (args) =>
            {
                bool requestIdPresent = TryGetUserProperty(args.UserProperties, faultRequestIdName, out string? requestIdValue);
                if (!requestIdPresent || !SubscribeRequestsAlreadyReceived.Contains(requestIdValue!))
                {
                    if (requestIdValue != null)
                    {
                        SubscribeRequestsAlreadyReceived.Add(requestIdValue);
                    }

                    FaultHandling faultHandling = await CloseConnectionIfRequested(args.UserProperties, args.ClientId);
                    if (faultHandling.FaultHandled)
                    {
                        args.ProcessSubscription = faultHandling.SendAck;
                        return;
                    }
                    else if (TryGetUserProperty(args.UserProperties, rejectSubscribeFaultName, out string? rejectSubscribeReasonString))
                    {
                        if (int.TryParse(rejectSubscribeReasonString, out int subackCodeInt))
                        {
                            try
                            {
                                args.Response.ReasonCode = (MqttSubscribeReasonCode)subackCodeInt;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Failed to convert the provided suback code int into a a valid suback code enum value. No fault will be triggered.");
                                return;
                            }
                        }
                    }
                }
            };

            _mqttServer.InterceptingUnsubscriptionAsync += async (args) =>
            {
                bool requestIdPresent = TryGetUserProperty(args.UserProperties, faultRequestIdName, out string? requestIdValue);
                if (!requestIdPresent || !UnsubscribeRequestsAlreadyReceived.Contains(requestIdValue!))
                {
                    if (requestIdValue != null)
                    {
                        UnsubscribeRequestsAlreadyReceived.Add(requestIdValue);
                    }

                    FaultHandling faultHandling = await CloseConnectionIfRequested(args.UserProperties, args.ClientId);
                    if (faultHandling.FaultHandled)
                    {
                        args.ProcessUnsubscription = faultHandling.SendAck;
                        return;
                    }
                    else if (TryGetUserProperty(args.UserProperties, rejectUnsubscribeFaultName, out string? rejectUnsubscribeReasonString))
                    {
                        if (int.TryParse(rejectUnsubscribeReasonString, out int subackCodeInt))
                        {
                            try
                            {
                                args.Response.ReasonCode = (MqttUnsubscribeReasonCode)subackCodeInt;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Failed to convert the provided unsuback code int into a a valid unsuback code enum value. No fault will be triggered.");
                                return;
                            }
                        }
                    }
                }
            };

            _mqttServer.ValidatingConnectionAsync += (args) =>
            {
                bool requestIdPresent = TryGetUserProperty(args.UserProperties, faultRequestIdName, out string? requestIdValue);
                if (!requestIdPresent || !ConnectRequestsAlreadyReceived.Contains(requestIdValue!))
                {
                    if (requestIdValue != null)
                    {
                        ConnectRequestsAlreadyReceived.Add(requestIdValue);
                    }

                    if (TryGetUserProperty(args.UserProperties, rejectConnectFaultName, out string? rejectConnectionFaultReasonString))
                    {
                        if (int.TryParse(rejectConnectionFaultReasonString, out int connackCodeInt))
                        {
                            try
                            {
                                args.ReasonCode = (MqttConnectReasonCode)connackCodeInt;
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Failed to convert the provided unsuback code int into a a valid unsuback code enum value. No fault will be triggered.");
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            };
        }

        private async Task<FaultHandling> CloseConnectionIfRequested(List<MqttUserProperty> properties, string clientId)
        {
            if (TryGetUserProperty(properties, disconnectFaultName, out string? disconnectFaultReasonString))
            {
                if (int.TryParse(disconnectFaultReasonString, out int disconnectCode))
                {
                    MqttDisconnectReasonCode disconnectReasonCode;

                    try
                    {
                        disconnectReasonCode = (MqttDisconnectReasonCode)disconnectCode;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to convert the provided disconnect code int into a a valid disconnect code enum value. No fault will be triggered.");
                        return new FaultHandling()
                        {
                            FaultHandled = false,
                            SendAck = true
                        };
                    }

                    if (TryGetFaultDelay(properties, out TimeSpan? faultDelay))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(faultDelay!.Value);
                                await _mqttServer.DisconnectClientAsync(clientId, disconnectReasonCode);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Failed to cause a fault. Exception: {0}", ex);
                            }
                        });

                        return new FaultHandling()
                        {
                            FaultHandled = true,
                            SendAck = true
                        };
                    }
                    else
                    {
                        // The request didn't specify a delay for the fault, so kill the connection immediately before acknowledging the request
                        await _mqttServer.DisconnectClientAsync(clientId, disconnectReasonCode);
                        return new FaultHandling()
                        {
                            FaultHandled = true,
                            SendAck = false
                        };
                    }
                }
            }

            return new FaultHandling()
            {
                FaultHandled = false,
                SendAck = true
            };
        }

        public bool TryGetUserProperty(List<MqttUserProperty> properties, string name, out string? value)
        {
            value = null;

            if (properties == null)
            {
                return false;
            }

            foreach (MqttUserProperty userProperty in properties)
            {
                if (userProperty.Name.Equals(name))
                {
                    value = userProperty.Value;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFaultDelay(List<MqttUserProperty> properties, out TimeSpan? value)
        {
            value = null;

            if (properties == null)
            {
                return false;
            }

            if (TryGetUserProperty(properties, faultDelayName, out string? faultDelayString))
            {
                if (int.TryParse(faultDelayString, out int delayInt))
                {
                    value = TimeSpan.FromSeconds(delayInt);
                    return true;
                }
            }

            return false;
        }

        public async Task StartAsync()
        {
            await _mqttServer.StartAsync();
        }

        public async Task StopAsync()
        {
            await _mqttServer.StopAsync();
        }

        class ConsoleLogger : IMqttNetLogger
        {
            readonly object _consoleSyncRoot = new();

            public bool IsEnabled => true;

            public void Publish(MqttNetLogLevel logLevel, string source, string message, object[]? parameters, Exception? exception)
            {
                var foregroundColor = ConsoleColor.White;
                switch (logLevel)
                {
                    case MqttNetLogLevel.Verbose:
                        foregroundColor = ConsoleColor.White;
                        break;

                    case MqttNetLogLevel.Info:
                        foregroundColor = ConsoleColor.Green;
                        break;

                    case MqttNetLogLevel.Warning:
                        foregroundColor = ConsoleColor.DarkYellow;
                        break;

                    case MqttNetLogLevel.Error:
                        foregroundColor = ConsoleColor.Red;
                        break;
                }

                if (parameters?.Length > 0)
                {
                    message = string.Format(message, parameters);
                }

                lock (_consoleSyncRoot)
                {
                    Console.ForegroundColor = foregroundColor;
                    Console.WriteLine(message);

                    if (exception != null)
                    {
                        Console.WriteLine(exception);
                    }
                }
            }
        }
    }
}
