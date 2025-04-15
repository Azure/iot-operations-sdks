// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    /// <summary>
    /// Utility class for converting commonly used MQTTNet types to and from generic types.
    /// </summary>
    internal static class MqttNetConverter
    {
        internal static MQTTnet.MqttEnhancedAuthenticationExchangeData FromGeneric(MqttEnhancedAuthenticationExchangeData generic)
        {
            var mqttNetData = new MQTTnet.MqttEnhancedAuthenticationExchangeData()
            {
                AuthenticationData = generic.AuthenticationData,
                ReasonCode = (MQTTnet.Protocol.MqttAuthenticateReasonCode)generic.ReasonCode,
                ReasonString = generic.ReasonString,
            };

            if (generic.UserProperties != null)
            {
                foreach (var userProperty in generic.UserProperties)
                {
                    mqttNetData.UserProperties.Add(new MQTTnet.Packets.MqttUserProperty(userProperty.Name, userProperty.Value));
                }
            }

            return mqttNetData;
        }

        internal static MqttTopicFilter ToGeneric(MQTTnet.Packets.MqttTopicFilter mqttNetTopicFilter)
        {
            return new MqttTopicFilter(mqttNetTopicFilter.Topic)
            {
                QualityOfServiceLevel = (MqttQualityOfServiceLevel)(int)mqttNetTopicFilter.QualityOfServiceLevel,
                NoLocal = mqttNetTopicFilter.NoLocal,
                RetainHandling = (MqttRetainHandling)(int)mqttNetTopicFilter.RetainHandling,
                RetainAsPublished = mqttNetTopicFilter.RetainAsPublished,
            };
        }

        internal static MqttApplicationMessage ToGeneric(MQTTnet.MqttApplicationMessage mqttNetMessage)
        {
            var genericMessage = new MqttApplicationMessage(mqttNetMessage.Topic)
            {
                QualityOfServiceLevel = (MqttQualityOfServiceLevel)(int)mqttNetMessage.QualityOfServiceLevel,
                ContentType = mqttNetMessage.ContentType,
                CorrelationData = mqttNetMessage.CorrelationData,
                MessageExpiryInterval = mqttNetMessage.MessageExpiryInterval,
                Payload = mqttNetMessage.Payload,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)mqttNetMessage.PayloadFormatIndicator,
                Retain = mqttNetMessage.Retain,
                SubscriptionIdentifiers = mqttNetMessage.SubscriptionIdentifiers,
                TopicAlias = mqttNetMessage.TopicAlias,
                ResponseTopic = mqttNetMessage.ResponseTopic,
                Dup = mqttNetMessage.Dup,
                UserProperties = ToGeneric(mqttNetMessage.UserProperties)
            };

            return genericMessage;
        }

        internal static MQTTnet.MqttApplicationMessage FromGeneric(MqttApplicationMessage applicationMessage)
        {
            var mqttNetMessageBuilder = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopicAlias(applicationMessage.TopicAlias)
                .WithTopic(applicationMessage.Topic)
                .WithContentType(applicationMessage.ContentType)
                .WithCorrelationData(applicationMessage.CorrelationData)
                .WithMessageExpiryInterval(applicationMessage.MessageExpiryInterval)
                .WithPayload(applicationMessage.Payload)
                .WithPayloadFormatIndicator(applicationMessage.PayloadFormatIndicator == MqttPayloadFormatIndicator.Unspecified ? MQTTnet.Protocol.MqttPayloadFormatIndicator.Unspecified : MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData)
                .WithQualityOfServiceLevel(applicationMessage.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce ? MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce : applicationMessage.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce ? MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce : MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .WithResponseTopic(applicationMessage.ResponseTopic)
                .WithRetainFlag(applicationMessage.Retain);

            if (applicationMessage.SubscriptionIdentifiers != null)
            {
                foreach (var subscriptionIdentifier in applicationMessage.SubscriptionIdentifiers)
                {
                    mqttNetMessageBuilder.WithSubscriptionIdentifier(subscriptionIdentifier);
                }
            }

            if (applicationMessage.UserProperties != null)
            {
                foreach (var userProperty in applicationMessage.UserProperties)
                {
                    mqttNetMessageBuilder.WithUserProperty(userProperty.Name, userProperty.Value);
                }
            }

            return mqttNetMessageBuilder.Build();
        }

        internal static List<MqttUserProperty> ToGeneric(IReadOnlyCollection<MQTTnet.Packets.MqttUserProperty> mqttNetUserProperties)
        {
            List<MqttUserProperty> genericUserProperties = new();
            if (mqttNetUserProperties != null)
            {
                foreach (var mqttNetUserProperty in mqttNetUserProperties)
                {
                    genericUserProperties.Add(new MqttUserProperty(mqttNetUserProperty.Name, mqttNetUserProperty.Value));
                }
            }

            return genericUserProperties;
        }

        internal static List<MQTTnet.Packets.MqttUserProperty>? FromGeneric(List<MqttUserProperty>? genericUserProperties)
        {
            if (genericUserProperties == null)
            {
                return null;
            }

            List<MQTTnet.Packets.MqttUserProperty> mqttNetUserProperties = new List<MQTTnet.Packets.MqttUserProperty>();
            foreach (var mqttNetUserProperty in genericUserProperties)
            {
                mqttNetUserProperties.Add(new MQTTnet.Packets.MqttUserProperty(mqttNetUserProperty.Name, mqttNetUserProperty.Value));
            }

            return mqttNetUserProperties;
        }

        internal static MQTTnet.MqttClientOptions FromGeneric(MqttClientOptions genericOptions, MQTTnet.IMqttClient underlyingClient)
        {
            var mqttNetOptions = new MQTTnet.MqttClientOptions
            {
                AllowPacketFragmentation = genericOptions.AllowPacketFragmentation,
                AuthenticationData = genericOptions.AuthenticationData,
                AuthenticationMethod = genericOptions.AuthenticationMethod,
                ChannelOptions = FromGeneric(genericOptions.ChannelOptions),
                CleanSession = genericOptions.CleanSession,
                ClientId = genericOptions.ClientId,
                Credentials = genericOptions.Credentials != null ? new MqttNetMqttClientCredentialsProvider(genericOptions.Credentials, underlyingClient) : null,
                EnhancedAuthenticationHandler = genericOptions.EnhancedAuthenticationHandler != null ? new MqttNetMqttEnhancedAuthenticationHandler(genericOptions.EnhancedAuthenticationHandler) : null,
                KeepAlivePeriod = genericOptions.KeepAlivePeriod,
                MaximumPacketSize = genericOptions.MaximumPacketSize,
                ProtocolVersion = (MQTTnet.Formatter.MqttProtocolVersion)genericOptions.ProtocolVersion,
                RequestProblemInformation = genericOptions.RequestProblemInformation,
                RequestResponseInformation = genericOptions.RequestResponseInformation,
                SessionExpiryInterval = genericOptions.SessionExpiryInterval,
                Timeout = genericOptions.Timeout,
                TopicAliasMaximum = genericOptions.TopicAliasMaximum,
                TryPrivate = genericOptions.TryPrivate,
                UserProperties = FromGeneric(genericOptions.UserProperties),
                ValidateFeatures = genericOptions.ValidateFeatures,
                WillContentType = genericOptions.WillContentType,
                WillDelayInterval = genericOptions.WillDelayInterval,
                WillCorrelationData = genericOptions.WillCorrelationData,
                WillMessageExpiryInterval = genericOptions.WillMessageExpiryInterval,
                WillPayload = genericOptions.WillPayload,
                WillPayloadFormatIndicator = (MQTTnet.Protocol.MqttPayloadFormatIndicator)genericOptions.WillPayloadFormatIndicator,
                WillQualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)genericOptions.WillQualityOfServiceLevel,
                WillTopic = genericOptions.WillTopic,
                WillResponseTopic = genericOptions.WillResponseTopic,
                WillRetain = genericOptions.WillRetain,
                WillUserProperties = FromGeneric(genericOptions.WillUserProperties),
                WriterBufferSize = genericOptions.WriterBufferSize,
                WriterBufferSizeMax = genericOptions.WriterBufferSizeMax
            };

            if (genericOptions.ReceiveMaximum != null)
            {
                mqttNetOptions.ReceiveMaximum = genericOptions.ReceiveMaximum.Value;
            }

            return mqttNetOptions;
        }

        internal static MqttClientOptions ToGeneric(MQTTnet.MqttClientOptions mqttNetOptions, MQTTnet.IMqttClient underlyingClient)
        {
            var genericChannelOptions = ToGeneric(mqttNetOptions.ChannelOptions);
            MqttClientOptions genericOptions = genericChannelOptions is MqttClientTcpOptions tcpOptions
                ? new MqttClientOptions(tcpOptions)
                : genericChannelOptions is MqttClientWebSocketOptions websocketOptions
                    ? new MqttClientOptions(websocketOptions)
                    : throw new NotSupportedException("Unsupported channel options provided");
            genericOptions.AllowPacketFragmentation = mqttNetOptions.AllowPacketFragmentation;
            genericOptions.AuthenticationData = mqttNetOptions.AuthenticationData;
            genericOptions.AuthenticationMethod = mqttNetOptions.AuthenticationMethod;
            genericOptions.CleanSession = mqttNetOptions.CleanSession;
            genericOptions.ClientId = mqttNetOptions.ClientId;
            genericOptions.KeepAlivePeriod = mqttNetOptions.KeepAlivePeriod;
            genericOptions.MaximumPacketSize = mqttNetOptions.MaximumPacketSize;
            genericOptions.ProtocolVersion = (MqttProtocolVersion)mqttNetOptions.ProtocolVersion;
            genericOptions.ReceiveMaximum = mqttNetOptions.ReceiveMaximum;
            genericOptions.RequestProblemInformation = mqttNetOptions.RequestProblemInformation;
            genericOptions.RequestResponseInformation = mqttNetOptions.RequestResponseInformation;
            genericOptions.SessionExpiryInterval = mqttNetOptions.SessionExpiryInterval;
            genericOptions.Timeout = mqttNetOptions.Timeout;
            genericOptions.TopicAliasMaximum = mqttNetOptions.TopicAliasMaximum;
            genericOptions.TryPrivate = mqttNetOptions.TryPrivate;
            genericOptions.UserProperties = ToGeneric(mqttNetOptions.UserProperties);
            genericOptions.ValidateFeatures = mqttNetOptions.ValidateFeatures;
            genericOptions.WillContentType = mqttNetOptions.WillContentType;
            genericOptions.WillDelayInterval = mqttNetOptions.WillDelayInterval;
            genericOptions.WillCorrelationData = mqttNetOptions.WillCorrelationData;
            genericOptions.WillMessageExpiryInterval = mqttNetOptions.WillMessageExpiryInterval;
            genericOptions.WillPayload = mqttNetOptions.WillPayload;
            genericOptions.WillPayloadFormatIndicator = (MqttPayloadFormatIndicator)mqttNetOptions.WillPayloadFormatIndicator;
            genericOptions.WillQualityOfServiceLevel = (MqttQualityOfServiceLevel)mqttNetOptions.WillQualityOfServiceLevel;
            genericOptions.WillTopic = mqttNetOptions.WillTopic;
            genericOptions.WillResponseTopic = mqttNetOptions.WillResponseTopic;
            genericOptions.WillRetain = mqttNetOptions.WillRetain;
            genericOptions.WillUserProperties = ToGeneric(mqttNetOptions.WillUserProperties);
            genericOptions.WriterBufferSize = mqttNetOptions.WriterBufferSize;
            genericOptions.WriterBufferSizeMax = mqttNetOptions.WriterBufferSizeMax;

            if (mqttNetOptions.Credentials != null)
            { 
                genericOptions.Credentials = new GenericMqttClientCredentialsProvider(mqttNetOptions.Credentials, underlyingClient);
            }

            if (mqttNetOptions.EnhancedAuthenticationHandler != null)
            { 
                genericOptions.EnhancedAuthenticationHandler = new GenericMqttExtendedAuthenticationExchangeHandler(mqttNetOptions.EnhancedAuthenticationHandler, underlyingClient);
            }

            return genericOptions;
        }

        internal static MqttClientConnectResult? ToGeneric(MQTTnet.MqttClientConnectResult? mqttNetConnectResult)
        {
            if (mqttNetConnectResult == null)
            {
                return null;
            }

            return new MqttClientConnectResult()
            {
                AssignedClientIdentifier = mqttNetConnectResult.AssignedClientIdentifier,
                AuthenticationMethod = mqttNetConnectResult.AuthenticationMethod,
                AuthenticationData = mqttNetConnectResult.AuthenticationData,
                IsSessionPresent = mqttNetConnectResult.IsSessionPresent,
                MaximumPacketSize = mqttNetConnectResult.MaximumPacketSize,
                MaximumQoS = (MqttQualityOfServiceLevel)mqttNetConnectResult.MaximumQoS,
                ReasonString = mqttNetConnectResult.ReasonString,
                ReceiveMaximum = mqttNetConnectResult.ReceiveMaximum,
                ResponseInformation = mqttNetConnectResult.ResponseInformation,
                RetainAvailable = mqttNetConnectResult.RetainAvailable,
                ResultCode = (MqttClientConnectResultCode)mqttNetConnectResult.ResultCode,
                ServerKeepAlive = mqttNetConnectResult.ServerKeepAlive,
                ServerReference = mqttNetConnectResult.ServerReference,
                SessionExpiryInterval = mqttNetConnectResult.SessionExpiryInterval,
                SharedSubscriptionAvailable = mqttNetConnectResult.SharedSubscriptionAvailable,
                SubscriptionIdentifiersAvailable = mqttNetConnectResult.SubscriptionIdentifiersAvailable,
                TopicAliasMaximum = mqttNetConnectResult.TopicAliasMaximum,
                UserProperties = ToGeneric(mqttNetConnectResult.UserProperties),
                WildcardSubscriptionAvailable = mqttNetConnectResult.WildcardSubscriptionAvailable,
            };
        }

        internal static IMqttClientChannelOptions ToGeneric(MQTTnet.IMqttClientChannelOptions mqttNetOptions)
        {
            if (mqttNetOptions is MQTTnet.MqttClientTcpOptions mqttNetTcpOptions)
            {
                return new MqttClientTcpOptions(((DnsEndPoint)mqttNetTcpOptions.RemoteEndpoint).Host, ((DnsEndPoint)mqttNetTcpOptions.RemoteEndpoint).Port)
                {
                    AddressFamily = mqttNetTcpOptions.AddressFamily,
                    BufferSize = mqttNetTcpOptions.BufferSize,
                    DualMode = mqttNetTcpOptions.DualMode,
                    LingerState = mqttNetTcpOptions.LingerState,
                    LocalEndpoint = mqttNetTcpOptions.LocalEndpoint,
                    NoDelay = mqttNetTcpOptions.NoDelay,
                    ProtocolType = mqttNetTcpOptions.ProtocolType,
                    TlsOptions = ToGeneric(mqttNetTcpOptions.TlsOptions)
                };
            }
            else if (mqttNetOptions is MQTTnet.MqttClientWebSocketOptions mqttNetWebsocketOptions)
            {
                return new MqttClientWebSocketOptions()
                {
                    CookieContainer = mqttNetWebsocketOptions.CookieContainer,
                    Credentials = mqttNetWebsocketOptions.Credentials,
                    KeepAliveInterval = mqttNetWebsocketOptions.KeepAliveInterval,
                    ProxyOptions = ToGeneric(mqttNetWebsocketOptions.ProxyOptions),
                    RequestHeaders = mqttNetWebsocketOptions.RequestHeaders,
                    SubProtocols = mqttNetWebsocketOptions.SubProtocols,
                    TlsOptions = ToGeneric(mqttNetWebsocketOptions.TlsOptions),
                    Uri = mqttNetWebsocketOptions.Uri,
                    UseDefaultCredentials = mqttNetWebsocketOptions.UseDefaultCredentials
                };
            }
            else
            {
                // MQTTnet doesn't support other implementations of this interface, so we won't either.
                throw new NotSupportedException("Unrecognized client channel options");
            }
        }

        internal static MQTTnet.IMqttClientChannelOptions FromGeneric(IMqttClientChannelOptions genericOptions)
        {
            if (genericOptions is MqttClientTcpOptions genericNetTcpOptions)
            {
                return new MQTTnet.MqttClientTcpOptions()
                {
                    AddressFamily = genericNetTcpOptions.AddressFamily,
                    BufferSize = genericNetTcpOptions.BufferSize,
                    DualMode = genericNetTcpOptions.DualMode,
                    LingerState = genericNetTcpOptions.LingerState,
                    LocalEndpoint = genericNetTcpOptions.LocalEndpoint,
                    NoDelay = genericNetTcpOptions.NoDelay,
                    ProtocolType = genericNetTcpOptions.ProtocolType,
                    RemoteEndpoint = new DnsEndPoint(genericNetTcpOptions.Host, genericNetTcpOptions.Port),
                    TlsOptions = FromGeneric(genericNetTcpOptions.TlsOptions)
                };
            }
            else if (genericOptions is MqttClientWebSocketOptions genericNetWebsocketOptions)
            {
                return new MQTTnet.MqttClientWebSocketOptions()
                {
                    CookieContainer = genericNetWebsocketOptions.CookieContainer,
                    Credentials = genericNetWebsocketOptions.Credentials,
                    KeepAliveInterval = genericNetWebsocketOptions.KeepAliveInterval,
                    ProxyOptions = FromGeneric(genericNetWebsocketOptions.ProxyOptions),
                    RequestHeaders = genericNetWebsocketOptions.RequestHeaders,
                    SubProtocols = genericNetWebsocketOptions.SubProtocols,
                    TlsOptions = FromGeneric(genericNetWebsocketOptions.TlsOptions),
                    Uri = genericNetWebsocketOptions.Uri,
                    UseDefaultCredentials = genericNetWebsocketOptions.UseDefaultCredentials
                };
            }
            else
            {
                // MQTTnet doesn't support other implementations of this interface, so we won't either.
                throw new NotSupportedException("Unrecognized client channel options");
            }
        }

        internal static MqttClientTcpOptions ToGeneric(MQTTnet.MqttClientTcpOptions mqttNetOptions)
        {
            return new MqttClientTcpOptions(((DnsEndPoint)mqttNetOptions.RemoteEndpoint).Host, ((DnsEndPoint)mqttNetOptions.RemoteEndpoint).Port)
            {
                AddressFamily = mqttNetOptions.AddressFamily,
                BufferSize = mqttNetOptions.BufferSize,
                DualMode = mqttNetOptions.DualMode,
                LingerState = mqttNetOptions.LingerState,
                LocalEndpoint = mqttNetOptions.LocalEndpoint,
                NoDelay = mqttNetOptions.NoDelay,
                ProtocolType = mqttNetOptions.ProtocolType,
                TlsOptions = ToGeneric(mqttNetOptions.TlsOptions)
            };
        }

        internal static MQTTnet.MqttClientTcpOptions FromGeneric(MqttClientTcpOptions genericOptions)
        {
            return new MQTTnet.MqttClientTcpOptions()
            {
                AddressFamily = genericOptions.AddressFamily,
                BufferSize = genericOptions.BufferSize,
                DualMode = genericOptions.DualMode,
                LingerState = genericOptions.LingerState,
                LocalEndpoint = genericOptions.LocalEndpoint,
                NoDelay = genericOptions.NoDelay,
                ProtocolType = genericOptions.ProtocolType,
                RemoteEndpoint = new DnsEndPoint(genericOptions.Host, genericOptions.Port),
                TlsOptions = FromGeneric(genericOptions.TlsOptions)
            };
        }

        internal static MQTTnet.MqttClientTlsOptions FromGeneric(MqttClientTlsOptions generic)
        {
            return new MQTTnet.MqttClientTlsOptions()
            {
                AllowRenegotiation = generic.AllowRenegotiation,
                AllowUntrustedCertificates = generic.AllowUntrustedCertificates,
                ApplicationProtocols = generic.ApplicationProtocols,
                CertificateSelectionHandler = generic.CertificateSelectionHandler != null ? new MqttNetCertificateSelectionHandler(generic.CertificateSelectionHandler).HandleCertificateSelection : null,
                CertificateValidationHandler = generic.CertificateValidationHandler != null ? new MqttNetCertificateValidationHandler(generic.CertificateValidationHandler).HandleCertificateValidation : null,
                CipherSuitesPolicy = generic.CipherSuitesPolicy,
                ClientCertificatesProvider = generic.ClientCertificatesProvider != null ? new MqttNetMqttClientCertificatesProvider(generic.ClientCertificatesProvider) : null,
                EncryptionPolicy = generic.EncryptionPolicy,
                IgnoreCertificateChainErrors = generic.IgnoreCertificateChainErrors,
                IgnoreCertificateRevocationErrors = generic.IgnoreCertificateRevocationErrors,
                RevocationMode = generic.RevocationMode,
                SslProtocol = generic.SslProtocol,
                TargetHost = generic.TargetHost,
                TrustChain = generic.TrustChain,
                UseTls = generic.UseTls,
            };
        }

        internal static MqttClientTlsOptions ToGeneric(MQTTnet.MqttClientTlsOptions mqttNetOptions)
        {
            return new MqttClientTlsOptions()
            {
                AllowRenegotiation = mqttNetOptions.AllowRenegotiation,
                AllowUntrustedCertificates = mqttNetOptions.AllowUntrustedCertificates,
                ApplicationProtocols = mqttNetOptions.ApplicationProtocols,
                CertificateSelectionHandler = mqttNetOptions.CertificateSelectionHandler != null ? new GenericCertificateSelectionHandler(mqttNetOptions.CertificateSelectionHandler).HandleCertificateSelection : null,
                CertificateValidationHandler = mqttNetOptions.CertificateValidationHandler != null ? new GenericCertificateValidationHandler(mqttNetOptions.CertificateValidationHandler).HandleCertificateValidation : null,
                CipherSuitesPolicy = mqttNetOptions.CipherSuitesPolicy,
                ClientCertificatesProvider = mqttNetOptions.ClientCertificatesProvider != null ? new GenericMqttClientCertificatesProvider(mqttNetOptions.ClientCertificatesProvider) : null,
                EncryptionPolicy = mqttNetOptions.EncryptionPolicy,
                IgnoreCertificateChainErrors = mqttNetOptions.IgnoreCertificateChainErrors,
                IgnoreCertificateRevocationErrors = mqttNetOptions.IgnoreCertificateRevocationErrors,
                RevocationMode = mqttNetOptions.RevocationMode,
                SslProtocol = mqttNetOptions.SslProtocol,
                TargetHost = mqttNetOptions.TargetHost,
                TrustChain = mqttNetOptions.TrustChain,
                UseTls = mqttNetOptions.UseTls,
            };
        }

        internal static MQTTnet.MqttClientDisconnectOptions FromGeneric(MqttClientDisconnectOptions genericOptions)
        {
            MQTTnet.MqttClientDisconnectOptions mqttNetOptions = new MQTTnet.MqttClientDisconnectOptions
            {
                SessionExpiryInterval = genericOptions.SessionExpiryInterval,
                ReasonString = genericOptions.ReasonString,
                Reason = (MQTTnet.MqttClientDisconnectOptionsReason)genericOptions.Reason,
                UserProperties = FromGeneric(genericOptions.UserProperties)
            };
            return mqttNetOptions;
        }

        internal static MqttClientConnectedEventArgs ToGeneric(MQTTnet.MqttClientConnectedEventArgs args)
        {
            Debug.Assert(args.ConnectResult != null);
            var generic = ToGeneric(args.ConnectResult);
            Debug.Assert(generic != null);
            return new MqttClientConnectedEventArgs(generic);
        }

        internal static MqttClientDisconnectedEventArgs ToGeneric(MQTTnet.MqttClientDisconnectedEventArgs args)
        {
            return new MqttClientDisconnectedEventArgs(
                args.ClientWasConnected,
                ToGeneric(args.ConnectResult),
                (MqttClientDisconnectReason)args.Reason,
                args.ReasonString,
                ToGeneric(args.UserProperties),
                args.Exception);
        }

        internal static MqttClientWebSocketProxyOptions ToGeneric(MQTTnet.MqttClientWebSocketProxyOptions options)
        {
            return new MqttClientWebSocketProxyOptions(options.Address)
            {
                BypassList = options.BypassList,
                BypassOnLocal = options.BypassOnLocal,
                Domain = options.Domain,
                Password = options.Password,
                UseDefaultCredentials = options.UseDefaultCredentials,
                Username = options.Username,
            };
        }

        internal static MQTTnet.MqttClientWebSocketProxyOptions? FromGeneric(MqttClientWebSocketProxyOptions? options)
        {
            if (options == null)
            {
                return null;
            }

            return new MQTTnet.MqttClientWebSocketProxyOptions()
            {
                Address = options.Address,
                BypassList = options.BypassList,
                BypassOnLocal = options.BypassOnLocal,
                Domain = options.Domain,
                Password = options.Password,
                UseDefaultCredentials = options.UseDefaultCredentials,
                Username = options.Username,
            };
        }

        internal static MQTTnet.MqttClientUnsubscribeOptions FromGeneric(MqttClientUnsubscribeOptions options)
        {
            MQTTnet.MqttClientUnsubscribeOptions mqttNetOptions = new();
            foreach (string topicFilter in options.TopicFilters)
            { 
                mqttNetOptions.TopicFilters.Add(topicFilter);
            }

            if (options.UserProperties != null)
            {
                mqttNetOptions.UserProperties = new();
                foreach (MqttUserProperty userProperty in options.UserProperties)
                {
                    mqttNetOptions.UserProperties.Add(new(userProperty.Name, userProperty.Value));
                }
            }

            return mqttNetOptions;
        }

        internal static MQTTnet.MqttClientSubscribeOptions FromGeneric(MqttClientSubscribeOptions options)
        {
            MQTTnet.MqttClientSubscribeOptions mqttNetOptions = new();
            foreach (MqttTopicFilter topicFilter in options.TopicFilters)
            {
                mqttNetOptions.TopicFilters.Add(new()
                { 
                    RetainAsPublished = topicFilter.RetainAsPublished,
                    NoLocal = topicFilter.NoLocal,
                    QualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)((int)topicFilter.QualityOfServiceLevel),
                    RetainHandling = (MQTTnet.Protocol.MqttRetainHandling)((int)topicFilter.RetainHandling),
                    Topic = topicFilter.Topic,
                });
            }

            if (options.UserProperties != null)
            {
                mqttNetOptions.UserProperties = new();
                foreach (MqttUserProperty userProperty in options.UserProperties)
                {
                    mqttNetOptions.UserProperties.Add(new(userProperty.Name, userProperty.Value));
                }
            }

            mqttNetOptions.SubscriptionIdentifier = options.SubscriptionIdentifier;

            return mqttNetOptions;
        }

        internal static MqttClientSubscribeResult ToGeneric(MQTTnet.MqttClientSubscribeResult result)
        {
            List<MqttClientSubscribeResultItem> genericItems = new();
            foreach (MQTTnet.MqttClientSubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientSubscribeResultItem(ToGeneric(mqttNetItem.TopicFilter), (MqttClientSubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new MqttClientSubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static MqttClientUnsubscribeResult ToGeneric(MQTTnet.MqttClientUnsubscribeResult result)
        {
            List<MqttClientUnsubscribeResultItem> genericItems = new();
            foreach (MQTTnet.MqttClientUnsubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientUnsubscribeResultItem(mqttNetItem.TopicFilter, (MqttClientUnsubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new MqttClientUnsubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));

        }

        internal static MqttClientPublishResult ToGeneric(MQTTnet.MqttClientPublishResult result)
        {
            return new MqttClientPublishResult(
                result.PacketIdentifier,
                (MqttClientPublishReasonCode)(int)result.ReasonCode,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static MqttApplicationMessageReceivedEventArgs ToGeneric(MQTTnet.MqttApplicationMessageReceivedEventArgs args, Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgementHandler)
        {
            return new MqttApplicationMessageReceivedEventArgs(
                args.ClientId,
                ToGeneric(args.ApplicationMessage),
                args.PacketIdentifier,
                acknowledgementHandler);
        }

        internal static Func<MQTTnet.MqttApplicationMessageReceivedEventArgs, Task> FromGeneric(Func<MqttApplicationMessageReceivedEventArgs, Task> genericFunc)
        {
            return new MqttNetHandler(genericFunc).Handle;
        }
    }
}
