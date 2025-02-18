﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using MQTTnet;
using System.Diagnostics;
using System.Net;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    /// <summary>
    /// Utility class for converting commonly used MQTTNet types to and from generic types.
    /// </summary>
    internal static class MqttNetConverter
    {
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

        internal static MQTTnet.MqttEnhancedAuthenticationExchangeData FromGeneric(Protocol.Models.MqttEnhancedAuthenticationExchangeData generic)
        {
            var mqttNet = new MQTTnet.MqttEnhancedAuthenticationExchangeData()
            {
                AuthenticationData = generic.AuthenticationData,
                ReasonString = generic.ReasonString,
                ReasonCode = (MQTTnet.Protocol.MqttAuthenticateReasonCode)(MqttAuthenticateReasonCode)((int)generic.ReasonCode),
            };

            if (generic.UserProperties != null)
            {
                foreach (var userProperty in generic.UserProperties)
                {
                    mqttNet.UserProperties.Add(new(userProperty.Name, userProperty.Value));
                }
            }

            return mqttNet;
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

        internal static Protocol.Models.MqttApplicationMessage ToGeneric(MQTTnet.MqttApplicationMessage mqttNetMessage)
        {
            var genericMessage = new Protocol.Models.MqttApplicationMessage(mqttNetMessage.Topic)
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
            };

            genericMessage.UserProperties = ToGeneric(mqttNetMessage.UserProperties);

            return genericMessage;
        }

        internal static MQTTnet.MqttApplicationMessage FromGeneric(Protocol.Models.MqttApplicationMessage applicationMessage)
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

        internal static MQTTnet.MqttClientOptions FromGeneric(Protocol.Models.MqttClientOptions genericOptions, MQTTnet.IMqttClient underlyingClient)
        {
            var mqttNetOptions = new MQTTnet.MqttClientOptions();

            mqttNetOptions.AllowPacketFragmentation = genericOptions.AllowPacketFragmentation;
            mqttNetOptions.AuthenticationData = genericOptions.AuthenticationData;
            mqttNetOptions.AuthenticationMethod = genericOptions.AuthenticationMethod;
            mqttNetOptions.ChannelOptions = FromGeneric(genericOptions.ChannelOptions);
            mqttNetOptions.CleanSession = genericOptions.CleanSession;
            mqttNetOptions.ClientId = genericOptions.ClientId;
            mqttNetOptions.Credentials = genericOptions.Credentials != null ? new MqttNetMqttClientCredentialsProvider(genericOptions.Credentials, underlyingClient) : null;
            mqttNetOptions.EnhancedAuthenticationHandler = genericOptions.EnhancedAuthenticationHandler != null ? new MqttNetMqttExtendedAuthenticationExchangeHandler(genericOptions.EnhancedAuthenticationHandler) : null;
            mqttNetOptions.KeepAlivePeriod = genericOptions.KeepAlivePeriod;
            mqttNetOptions.MaximumPacketSize = genericOptions.MaximumPacketSize;
            mqttNetOptions.ProtocolVersion = (MQTTnet.Formatter.MqttProtocolVersion)genericOptions.ProtocolVersion;
            mqttNetOptions.ReceiveMaximum = genericOptions.ReceiveMaximum;
            mqttNetOptions.RequestProblemInformation = genericOptions.RequestProblemInformation;
            mqttNetOptions.RequestResponseInformation = genericOptions.RequestResponseInformation;
            mqttNetOptions.SessionExpiryInterval = genericOptions.SessionExpiryInterval;
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

        internal static Protocol.Models.MqttClientOptions ToGeneric(MQTTnet.MqttClientOptions mqttNetOptions, MQTTnet.IMqttClient underlyingClient)
        {
            var genericChannelOptions = ToGeneric(mqttNetOptions.ChannelOptions);
            Protocol.Models.MqttClientOptions genericOptions;
            if (genericChannelOptions is Protocol.Models.MqttClientTcpOptions tcpOptions)
            {
                genericOptions = new Protocol.Models.MqttClientOptions(tcpOptions);
            }
            else if (genericChannelOptions is Protocol.Models.MqttClientWebSocketOptions websocketOptions)
            {
                genericOptions = new Protocol.Models.MqttClientOptions(websocketOptions);
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
                genericOptions.EnhancedAuthenticationHandler = new GenericMqttEnhancedAuthenticationHandler(mqttNetOptions.EnhancedAuthenticationHandler, underlyingClient);
            }

            return genericOptions;
        }

        internal static Protocol.Models.MqttClientConnectResult? ToGeneric(MQTTnet.MqttClientConnectResult? mqttNetConnectResult)
        {
            if (mqttNetConnectResult == null)
            {
                return null;
            }

            return new Protocol.Models.MqttClientConnectResult()
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
                ResultCode = (Protocol.Models.MqttClientConnectResultCode)mqttNetConnectResult.ResultCode,
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

        internal static Protocol.Models.IMqttClientChannelOptions ToGeneric(MQTTnet.IMqttClientChannelOptions mqttNetOptions)
        {
            if (mqttNetOptions is MQTTnet.MqttClientTcpOptions mqttNetTcpOptions)
            {
                return new Protocol.Models.MqttClientTcpOptions(((DnsEndPoint)mqttNetTcpOptions.RemoteEndpoint).Host, ((DnsEndPoint)mqttNetTcpOptions.RemoteEndpoint).Port)
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
                return new Protocol.Models.MqttClientWebSocketOptions()
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

        internal static MQTTnet.IMqttClientChannelOptions FromGeneric(Protocol.Models.IMqttClientChannelOptions genericOptions)
        {
            if (genericOptions is Protocol.Models.MqttClientTcpOptions genericNetTcpOptions)
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
            else if (genericOptions is Protocol.Models.MqttClientWebSocketOptions genericNetWebsocketOptions)
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

        internal static Protocol.Models.MqttClientTcpOptions ToGeneric(MQTTnet.MqttClientTcpOptions mqttNetOptions)
        {
            return new Protocol.Models.MqttClientTcpOptions(((DnsEndPoint)mqttNetOptions.RemoteEndpoint).Host, ((DnsEndPoint)mqttNetOptions.RemoteEndpoint).Port)
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

        internal static MQTTnet.MqttClientTcpOptions FromGeneric(Protocol.Models.MqttClientTcpOptions genericOptions)
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

        internal static MQTTnet.MqttClientTlsOptions FromGeneric(Protocol.Models.MqttClientTlsOptions generic)
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

        internal static Protocol.Models.MqttClientTlsOptions ToGeneric(MQTTnet.MqttClientTlsOptions mqttNetOptions)
        {
            return new Protocol.Models.MqttClientTlsOptions()
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

        internal static MQTTnet.MqttClientDisconnectOptions FromGeneric(Protocol.Models.MqttClientDisconnectOptions genericOptions)
        {
            MQTTnet.MqttClientDisconnectOptions mqttNetOptions = new MQTTnet.MqttClientDisconnectOptions();
            mqttNetOptions.SessionExpiryInterval = genericOptions.SessionExpiryInterval;
            mqttNetOptions.ReasonString = genericOptions.ReasonString;
            mqttNetOptions.Reason = (MQTTnet.MqttClientDisconnectOptionsReason)genericOptions.Reason;
            mqttNetOptions.UserProperties = FromGeneric(genericOptions.UserProperties);
            return mqttNetOptions;
        }

        internal static Protocol.Events.MqttClientConnectedEventArgs ToGeneric(MQTTnet.MqttClientConnectedEventArgs args)
        {
            Debug.Assert(args.ConnectResult != null);
            var generic = ToGeneric(args.ConnectResult);
            Debug.Assert(generic != null);
            return new Protocol.Events.MqttClientConnectedEventArgs(generic);
        }

        internal static Protocol.Events.MqttClientDisconnectedEventArgs ToGeneric(MQTTnet.MqttClientDisconnectedEventArgs args)
        {
            return new Protocol.Events.MqttClientDisconnectedEventArgs(
                args.ClientWasConnected,
                ToGeneric(args.ConnectResult),
                (Protocol.Models.MqttClientDisconnectReason)args.Reason,
                args.ReasonString,
                ToGeneric(args.UserProperties),
                args.Exception);
        }

        internal static Protocol.Models.MqttClientWebSocketProxyOptions ToGeneric(MQTTnet.MqttClientWebSocketProxyOptions options)
        {
            return new Protocol.Models.MqttClientWebSocketProxyOptions(options.Address)
            {
                BypassList = options.BypassList,
                BypassOnLocal = options.BypassOnLocal,
                Domain = options.Domain,
                Password = options.Password,
                UseDefaultCredentials = options.UseDefaultCredentials,
                Username = options.Username,
            };
        }

        internal static MQTTnet.MqttClientWebSocketProxyOptions? FromGeneric(Protocol.Models.MqttClientWebSocketProxyOptions? options)
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

        internal static MQTTnet.MqttClientUnsubscribeOptions FromGeneric(Protocol.Models.MqttClientUnsubscribeOptions options)
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

        internal static MQTTnet.MqttClientSubscribeOptions FromGeneric(Protocol.Models.MqttClientSubscribeOptions options)
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

        internal static Protocol.Models.MqttClientSubscribeResult ToGeneric(MQTTnet.MqttClientSubscribeResult result)
        {
            List<Protocol.Models.MqttClientSubscribeResultItem> genericItems = new();
            foreach (MQTTnet.MqttClientSubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new Protocol.Models.MqttClientSubscribeResultItem(ToGeneric(mqttNetItem.TopicFilter), (MqttClientSubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new Protocol.Models.MqttClientSubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static Protocol.Models.MqttClientUnsubscribeResult ToGeneric(MQTTnet.MqttClientUnsubscribeResult result)
        {
            List<Protocol.Models.MqttClientUnsubscribeResultItem> genericItems = new();
            foreach (MQTTnet.MqttClientUnsubscribeResultItem mqttNetItem in result.Items)
            {
                genericItems.Add(new Protocol.Models.MqttClientUnsubscribeResultItem(mqttNetItem.TopicFilter, (MqttClientUnsubscribeReasonCode)((int)mqttNetItem.ResultCode)));
            }

            return new Protocol.Models.MqttClientUnsubscribeResult(
                result.PacketIdentifier,
                genericItems,
                result.ReasonString,
                ToGeneric(result.UserProperties));

        }

        internal static Protocol.Models.MqttClientPublishResult ToGeneric(MQTTnet.MqttClientPublishResult result)
        {
            return new Protocol.Models.MqttClientPublishResult(
                result.PacketIdentifier,
                (Protocol.Models.MqttClientPublishReasonCode)(int)result.ReasonCode,
                result.ReasonString,
                ToGeneric(result.UserProperties));
        }

        internal static Protocol.Events.MqttApplicationMessageReceivedEventArgs ToGeneric(MQTTnet.MqttApplicationMessageReceivedEventArgs args, Func<Protocol.Events.MqttApplicationMessageReceivedEventArgs, CancellationToken, Task> acknowledgementHandler)
        {
            return new Protocol.Events.MqttApplicationMessageReceivedEventArgs(
                args.ClientId,
                ToGeneric(args.ApplicationMessage),
                args.PacketIdentifier,
                acknowledgementHandler);
        }

        internal static Func<MQTTnet.MqttApplicationMessageReceivedEventArgs, Task> FromGeneric(Func<Protocol.Events.MqttApplicationMessageReceivedEventArgs, Task> genericFunc)
        {
            return new MqttNetHandler(genericFunc).Handle;
        }
    }
}
