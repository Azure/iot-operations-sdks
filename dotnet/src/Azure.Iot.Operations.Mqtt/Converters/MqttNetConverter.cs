// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System.Diagnostics;
using System.Net;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    /// <summary>
    /// Utility class for converting commonly used MQTTNet types to and from generic types.
    /// </summary>
    internal static class MqttNetConverter
    {
        internal static MQTTnet.Client.MqttExtendedAuthenticationExchangeData FromGeneric(MqttExtendedAuthenticationExchangeData generic)
        {
            var mqttNetData = new MQTTnet.Client.MqttExtendedAuthenticationExchangeData()
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

        internal static MQTTnet.Packets.MqttTopicFilter FromGeneric(MqttTopicFilter genericTopicFilter)
        {
            var builder = new MQTTnet.MqttTopicFilterBuilder()
                .WithTopic(genericTopicFilter.Topic)
                .WithRetainHandling((MQTTnet.Protocol.MqttRetainHandling)(int)genericTopicFilter.RetainHandling)
                .WithNoLocal(genericTopicFilter.NoLocal)
                .WithRetainAsPublished(genericTopicFilter.RetainAsPublished);

            if (genericTopicFilter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
            {
                builder.WithAtMostOnceQoS();
            }
            else if (genericTopicFilter.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
            {
                builder.WithAtLeastOnceQoS();
            }
            else
            {
                builder.WithExactlyOnceQoS();
            }

            return builder.Build();
        }

        internal static MqttApplicationMessage ToGeneric(MQTTnet.MqttApplicationMessage mqttNetMessage)
        {
            var genericMessage = new MqttApplicationMessage(mqttNetMessage.Topic)
            {
                QualityOfServiceLevel = (MqttQualityOfServiceLevel)(int)mqttNetMessage.QualityOfServiceLevel,
                ContentType = mqttNetMessage.ContentType,
                CorrelationData = mqttNetMessage.CorrelationData,
                MessageExpiryInterval = mqttNetMessage.MessageExpiryInterval,
                PayloadSegment = mqttNetMessage.PayloadSegment,
                PayloadFormatIndicator = (MqttPayloadFormatIndicator)mqttNetMessage.PayloadFormatIndicator,
                Retain = mqttNetMessage.Retain,
                SubscriptionIdentifiers = mqttNetMessage.SubscriptionIdentifiers,
                TopicAlias = mqttNetMessage.TopicAlias,
                ResponseTopic = mqttNetMessage.ResponseTopic,
                Dup = mqttNetMessage.Dup,
            };

            genericMessage.UserProperties = ToGeneric(mqttNetMessage.UserProperties);

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
                .WithPayload(applicationMessage.PayloadSegment)
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

        internal static MQTTnet.Client.MqttClientOptions FromGeneric(MqttClientOptions genericOptions, MQTTnet.Client.IMqttClient underlyingClient)
        {
            var mqttNetOptions = new MQTTnet.Client.MqttClientOptions();

            mqttNetOptions.AllowPacketFragmentation = genericOptions.AllowPacketFragmentation;
            mqttNetOptions.AuthenticationData = genericOptions.AuthenticationData;
            mqttNetOptions.AuthenticationMethod = genericOptions.AuthenticationMethod;
            mqttNetOptions.ChannelOptions = FromGeneric(genericOptions.ChannelOptions);
            mqttNetOptions.CleanSession = genericOptions.CleanSession;
            mqttNetOptions.ClientId = genericOptions.ClientId;
            mqttNetOptions.Credentials = genericOptions.Credentials != null ? new MqttNetMqttClientCredentialsProvider(genericOptions.Credentials, underlyingClient) : null;
            mqttNetOptions.ExtendedAuthenticationExchangeHandler = genericOptions.ExtendedAuthenticationExchangeHandler != null ? new MqttNetMqttExtendedAuthenticationExchangeHandler(genericOptions.ExtendedAuthenticationExchangeHandler) : null;
            mqttNetOptions.KeepAlivePeriod = genericOptions.KeepAlivePeriod;
            mqttNetOptions.MaximumPacketSize = genericOptions.MaximumPacketSize;
            mqttNetOptions.ProtocolVersion = (MQTTnet.Formatter.MqttProtocolVersion)genericOptions.ProtocolVersion;
            mqttNetOptions.ReceiveMaximum = genericOptions.ReceiveMaximum;
            mqttNetOptions.RequestProblemInformation = genericOptions.RequestProblemInformation;
            mqttNetOptions.RequestResponseInformation = genericOptions.RequestResponseInformation;
            mqttNetOptions.SessionExpiryInterval = genericOptions.SessionExpiryInterval;
            mqttNetOptions.ThrowOnNonSuccessfulConnectResponse = genericOptions.ThrowOnNonSuccessfulConnectResponse;
            mqttNetOptions.Timeout = genericOptions.Timeout;
            mqttNetOptions.TopicAliasMaximum = genericOptions.TopicAliasMaximum;
            mqttNetOptions.TryPrivate = genericOptions.TryPrivate;
            mqttNetOptions.UserProperties = FromGeneric(genericOptions.UserProperties);
            mqttNetOptions.ValidateFeatures = genericOptions.ValidateFeatures;
            mqttNetOptions.WillContentType = genericOptions.WillContentType;
            mqttNetOptions.WillDelayInterval = genericOptions.WillDelayInterval;
            mqttNetOptions.WillCorrelationData = genericOptions.WillCorrelationData;
            mqttNetOptions.WillMessageExpiryInterval = genericOptions.WillMessageExpiryInterval;
            mqttNetOptions.WillPayload = genericOptions.WillPayload;
            mqttNetOptions.WillPayloadFormatIndicator = (MQTTnet.Protocol.MqttPayloadFormatIndicator)genericOptions.WillPayloadFormatIndicator;
            mqttNetOptions.WillQualityOfServiceLevel = (MQTTnet.Protocol.MqttQualityOfServiceLevel)genericOptions.WillQualityOfServiceLevel;
            mqttNetOptions.WillTopic = genericOptions.WillTopic;
            mqttNetOptions.WillResponseTopic = genericOptions.WillResponseTopic;
            mqttNetOptions.WillRetain = genericOptions.WillRetain;
            mqttNetOptions.WillUserProperties = FromGeneric(genericOptions.WillUserProperties);
            mqttNetOptions.WriterBufferSize = genericOptions.WriterBufferSize;
            mqttNetOptions.WriterBufferSizeMax = genericOptions.WriterBufferSizeMax;
            return mqttNetOptions;
        }

        internal static MqttClientOptions ToGeneric(MQTTnet.Client.MqttClientOptions mqttNetOptions, MQTTnet.Client.IMqttClient underlyingClient)
        {
            var genericChannelOptions = ToGeneric(mqttNetOptions.ChannelOptions);
            MqttClientOptions genericOptions;
            if (genericChannelOptions is MqttClientTcpOptions tcpOptions)
            {
                genericOptions = new MqttClientOptions(tcpOptions);
            }
            else if (genericChannelOptions is MqttClientWebSocketOptions websocketOptions)
            {
                genericOptions = new MqttClientOptions(websocketOptions);
            }
            else
            {
                throw new NotSupportedException("Unsupported channel options provided");
            }

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
            genericOptions.ThrowOnNonSuccessfulConnectResponse = mqttNetOptions.ThrowOnNonSuccessfulConnectResponse;
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

            if (mqttNetOptions.ExtendedAuthenticationExchangeHandler != null)
            { 
                genericOptions.ExtendedAuthenticationExchangeHandler = new GenericMqttExtendedAuthenticationExchangeHandler(mqttNetOptions.ExtendedAuthenticationExchangeHandler, underlyingClient);
            }

            return genericOptions;
        }

        internal static MqttClientConnectResult? ToGeneric(MQTTnet.Client.MqttClientConnectResult? mqttNetConnectResult)
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

        internal static IMqttClientChannelOptions ToGeneric(MQTTnet.Client.IMqttClientChannelOptions mqttNetOptions)
        {
            if (mqttNetOptions is MQTTnet.Client.MqttClientTcpOptions mqttNetTcpOptions)
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
            else if (mqttNetOptions is MQTTnet.Client.MqttClientWebSocketOptions mqttNetWebsocketOptions)
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

        internal static MQTTnet.Client.IMqttClientChannelOptions FromGeneric(IMqttClientChannelOptions genericOptions)
        {
            if (genericOptions is MqttClientTcpOptions genericNetTcpOptions)
            {
                return new MQTTnet.Client.MqttClientTcpOptions()
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
                return new MQTTnet.Client.MqttClientWebSocketOptions()
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

        internal static MqttClientTcpOptions ToGeneric(MQTTnet.Client.MqttClientTcpOptions mqttNetOptions)
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

        internal static MQTTnet.Client.MqttClientTcpOptions FromGeneric(MqttClientTcpOptions genericOptions)
        {
            return new MQTTnet.Client.MqttClientTcpOptions()
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

        internal static MQTTnet.Client.MqttClientTlsOptions FromGeneric(MqttClientTlsOptions generic)
        {
            return new MQTTnet.Client.MqttClientTlsOptions()
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

        internal static MqttExtendedAuthenticationExchangeContext ToGeneric(MQTTnet.Client.MqttExtendedAuthenticationExchangeContext mqttNetContext)
        {
            return new MqttExtendedAuthenticationExchangeContext((MqttAuthenticateReasonCode)mqttNetContext.ReasonCode)
            {
                AuthenticationData = mqttNetContext.AuthenticationData,
                AuthenticationMethod = mqttNetContext.AuthenticationMethod,
                ReasonString = mqttNetContext.ReasonString,
                UserProperties = ToGeneric(mqttNetContext.UserProperties),
            };
        }

        internal static MQTTnet.Client.MqttExtendedAuthenticationExchangeContext FromGeneric(MqttExtendedAuthenticationExchangeContext genericContext, MQTTnet.Client.IMqttClient underlyingClient)
        {
            return new MQTTnet.Client.MqttExtendedAuthenticationExchangeContext(
                new MQTTnet.Packets.MqttAuthPacket()
                {
                    AuthenticationData = genericContext.AuthenticationData,
                    AuthenticationMethod = genericContext.AuthenticationMethod,
                    ReasonCode = (MQTTnet.Protocol.MqttAuthenticateReasonCode)genericContext.ReasonCode,
                    ReasonString = genericContext.ReasonString,
                    UserProperties = FromGeneric(genericContext.UserProperties),
                },
                underlyingClient as MQTTnet.Client.MqttClient);
        }

        internal static MqttClientTlsOptions ToGeneric(MQTTnet.Client.MqttClientTlsOptions mqttNetOptions)
        {
            return new MqttClientTlsOptions()
            {
                AllowRenegotiation = mqttNetOptions.AllowRenegotiation,
                AllowUntrustedCertificates = mqttNetOptions.AllowUntrustedCertificates,
                ApplicationProtocols = mqttNetOptions.ApplicationProtocols,
                CertificateSelectionHandler = new GenericCertificateSelectionHandler(mqttNetOptions.CertificateSelectionHandler).HandleCertificateSelection,
                CertificateValidationHandler = new GenericCertificateValidationHandler(mqttNetOptions.CertificateValidationHandler).HandleCertificateValidation,
                CipherSuitesPolicy = mqttNetOptions.CipherSuitesPolicy,
                ClientCertificatesProvider = new GenericMqttClientCertificatesProvider(mqttNetOptions.ClientCertificatesProvider),
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

        internal static MQTTnet.Client.MqttClientDisconnectOptions FromGeneric(MqttClientDisconnectOptions genericOptions)
        {
            MQTTnet.Client.MqttClientDisconnectOptions mqttNetOptions = new MQTTnet.Client.MqttClientDisconnectOptions();
            mqttNetOptions.SessionExpiryInterval = genericOptions.SessionExpiryInterval;
            mqttNetOptions.ReasonString = genericOptions.ReasonString;
            mqttNetOptions.Reason = (MQTTnet.Client.MqttClientDisconnectOptionsReason)genericOptions.Reason;
            mqttNetOptions.UserProperties = FromGeneric(genericOptions.UserProperties);
            return mqttNetOptions;
        }

        internal static MqttClientConnectedEventArgs ToGeneric(MQTTnet.Client.MqttClientConnectedEventArgs args)
        {
            Debug.Assert(args.ConnectResult != null);
            var generic = ToGeneric(args.ConnectResult);
            Debug.Assert(generic != null);
            return new MqttClientConnectedEventArgs(generic);
        }

        internal static MqttClientDisconnectedEventArgs ToGeneric(MQTTnet.Client.MqttClientDisconnectedEventArgs args)
        {
            return new MqttClientDisconnectedEventArgs(
                args.ClientWasConnected,
                ToGeneric(args.ConnectResult),
                (MqttClientDisconnectReason)args.Reason,
                args.ReasonString,
                ToGeneric(args.UserProperties),
                args.Exception);
        }

        internal static MqttClientWebSocketProxyOptions ToGeneric(MQTTnet.Client.MqttClientWebSocketProxyOptions options)
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

        internal static MQTTnet.Client.MqttClientWebSocketProxyOptions? FromGeneric(MqttClientWebSocketProxyOptions? options)
        {
            if (options == null)
            {
                return null;
            }

            return new MQTTnet.Client.MqttClientWebSocketProxyOptions()
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

        internal static MQTTnet.Client.MqttClientUnsubscribeOptions FromGeneric(MqttClientUnsubscribeOptions options)
        {
            MQTTnet.Client.MqttClientUnsubscribeOptions mqttNetOptions = new();
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

        internal static MQTTnet.Client.MqttClientSubscribeOptions FromGeneric(MqttClientSubscribeOptions options)
        {
            MQTTnet.Client.MqttClientSubscribeOptions mqttNetOptions = new();
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

        internal static MqttClientSubscribeResult ToGeneric(MQTTnet.Client.MqttClientSubscribeResult result)
        {
            List<MqttClientSubscribeResultItem> genericItems = new();
            foreach (MQTTnet.Client.MqttClientSubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientSubscribeResultItem(ToGeneric(mqttNetItem.TopicFilter), (MqttClientSubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new MqttClientSubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static MqttClientUnsubscribeResult ToGeneric(MQTTnet.Client.MqttClientUnsubscribeResult result)
        {
            List<MqttClientUnsubscribeResultItem> genericItems = new();
            foreach (MQTTnet.Client.MqttClientUnsubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new MqttClientUnsubscribeResultItem(mqttNetItem.TopicFilter, (MqttClientUnsubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new MqttClientUnsubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));

        }

        internal static MqttClientPublishResult ToGeneric(MQTTnet.Client.MqttClientPublishResult result)
        {
            return new MqttClientPublishResult(
                result.PacketIdentifier,
                (MqttClientPublishReasonCode)(int)result.ReasonCode,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static MqttApplicationMessageReceivedEventArgs ToGeneric(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs args, Func<MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgementHandler)
        {
            return new MqttApplicationMessageReceivedEventArgs(
                args.ClientId,
                ToGeneric(args.ApplicationMessage),
                args.PacketIdentifier,
                acknowledgementHandler);
        }

        internal static Func<MQTTnet.Client.MqttApplicationMessageReceivedEventArgs, Task> FromGeneric(Func<MqttApplicationMessageReceivedEventArgs, Task> genericFunc)
        {
            return new MqttNetHandler(genericFunc).Handle;
        }
    }
}
