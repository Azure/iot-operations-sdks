// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientOptions
    {
        public MqttClientOptions(MqttClientTcpOptions tcpOptions)
        {
            ChannelOptions = tcpOptions;
        }

        public MqttClientOptions(MqttClientWebSocketOptions websocketOptions)
        {
            ChannelOptions = websocketOptions;
        }

        public MqttClientOptions(MqttConnectionSettings cs)
        {
            if (cs.ClientId != null)
            {
                ClientId = cs.ClientId;
            }

            KeepAlivePeriod = cs.KeepAlive;
            ProtocolVersion = MqttProtocolVersion.V500;
            CleanSession = cs.CleanStart;
            SessionExpiryInterval = (uint)cs.SessionExpiry.TotalSeconds;

            if (!string.IsNullOrEmpty(cs.Username))
            {
                Credentials = !string.IsNullOrEmpty(cs.PasswordFile)
                    ? new MqttClientCredentials(cs.Username, File.ReadAllBytes(cs.PasswordFile))
                    : (IMqttClientCredentialsProvider)new MqttClientCredentials(cs.Username);
            }

            if (!string.IsNullOrEmpty(cs.SatAuthFile))
            {
                AuthenticationMethod = "K8S-SAT";
                AuthenticationData = File.ReadAllBytes(cs.SatAuthFile);
                AddUserProperty("tokenFilePath", cs.SatAuthFile);
            }

            if (!cs.UseTls)
            {
                ChannelOptions = new MqttClientTcpOptions(cs.HostName, cs.TcpPort)
                {
                    TlsOptions = new MqttClientTlsOptions()
                    {
                        UseTls = false
                    }
                };
            }
            else
            {
                try
                {
                    MqttClientTlsOptions tlsParams = new()
                    {
                        SslProtocol = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                    };

                    X509Certificate2Collection caCerts = [];
                    if (cs.TrustChain != null)
                    {
                        tlsParams.TrustChain = cs.TrustChain;
                    }
                    else if (!string.IsNullOrEmpty(cs.CaFile))
                    {
                        caCerts.ImportFromPemFile(cs.CaFile);
                        tlsParams.TrustChain = caCerts;
                        tlsParams.RevocationMode = X509RevocationMode.NoCheck;
                    }

                    List<X509Certificate2> certs = [];
                    if (!string.IsNullOrEmpty(cs.CertFile) && !string.IsNullOrEmpty(cs.KeyFile))
                    {
                        X509Certificate2 cert = X509ClientCertificateLocator.Load(cs.CertFile, cs.KeyFile, cs.KeyPasswordFile);
                        if (!cert.HasPrivateKey)
                        {
                            throw new SecurityException("Provided certificate is missing the private key information.");
                        }
                        certs.Add(cert);
                    }

                    if (cs.ClientCertificate is not null)
                    {
                        certs.Add(cs.ClientCertificate);
                    }

                    tlsParams.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(certs);
                    tlsParams.UseTls = true;

                    ChannelOptions = new MqttClientTcpOptions(cs.HostName, cs.TcpPort)
                    {
                        TlsOptions = tlsParams,
                    };
                }
                catch (SecurityException ex) // cert is missing private key
                {
                    throw AkriMqttException.GetConfigurationInvalidException(nameof(MqttConnectionSettings), cs, ex.Message, ex);
                }
                catch (ArgumentException ex) // cert expired
                {
                    throw new AkriMqttException(ex.Message, ex)
                    {
                        Kind = AkriMqttErrorKind.StateInvalid,
                        InApplication = false,
                        IsShallow = true,
                        IsRemote = false,
                        PropertyName = nameof(MqttConnectionSettings),
                        PropertyValue = cs,
                    };
                }
            }
        }

        /// <summary>
        ///     Usually the MQTT packets can be send partially. This is done by using multiple TCP packets
        ///     or WebSocket frames etc. Unfortunately not all brokers (like Amazon Web Services (AWS)) do support this feature and
        ///     will close the connection when receiving such packets. If such a service is used this flag must
        ///     be set to _false_.
        /// </summary>
        public bool AllowPacketFragmentation { get; set; } = true;

        /// <summary>
        ///     Gets or sets the authentication data.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public byte[]? AuthenticationData { get; set; }

        /// <summary>
        ///     Gets or sets the authentication method.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string? AuthenticationMethod { get; set; }

        public IMqttClientChannelOptions ChannelOptions { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether clean sessions are used or not.
        ///     When a client connects to a broker it can connect using either a non persistent connection (clean session) or a
        ///     persistent connection.
        ///     With a non persistent connection the broker doesn't store any subscription information or undelivered messages for
        ///     the client.
        ///     This mode is ideal when the client only publishes messages.
        ///     It can also connect as a durable client using a persistent connection.
        ///     In this mode, the broker will store subscription information, and undelivered messages for the client.
        /// </summary>
        public bool CleanSession { get; set; } = true;

        /// <summary>
        ///     Gets the client identifier.
        ///     Hint: This identifier needs to be unique over all used clients / devices on the broker to avoid connection issues.
        /// </summary>
        public string ClientId { get; set; } = Guid.NewGuid().ToString("N");

        public IMqttClientCredentialsProvider? Credentials { get; set; }

        public IMqttExtendedAuthenticationExchangeHandler? ExtendedAuthenticationExchangeHandler { get; set; }

        /// <summary>
        ///     Gets or sets the keep alive period.
        ///     The connection is normally left open by the client so that is can send and receive data at any time.
        ///     If no data flows over an open connection for a certain time period then the client will generate a PINGREQ and
        ///     expect to receive a PINGRESP from the broker.
        ///     This message exchange confirms that the connection is open and working.
        ///     This period is known as the keep alive period.
        /// </summary>
        public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        ///     Gets or sets the maximum packet size.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint MaximumPacketSize { get; set; }

        public MqttProtocolVersion ProtocolVersion { get; set; } = MqttProtocolVersion.V500;

        /// <summary>
        ///     Gets or sets the receive maximum.
        ///     This gives the maximum length of the receive messages.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public ushort ReceiveMaximum { get; set; }

        /// <summary>
        ///     Gets or sets the request problem information.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public bool RequestProblemInformation { get; set; } = true;

        /// <summary>
        ///     Gets or sets the request response information.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public bool RequestResponseInformation { get; set; }

        /// <summary>
        ///     Gets or sets the session expiry interval.
        ///     The time after a session expires when it's not actively used.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint SessionExpiryInterval { get; set; }

        /// <summary>
        ///     Gets or sets whether an exception should be thrown when the server has sent a non success ACK packet.
        /// </summary>
        public bool ThrowOnNonSuccessfulConnectResponse { get; set; } = true;

        /// <summary>
        ///     Gets or sets the timeout which will be applied at socket level and internal operations.
        ///     The default value is the same as for sockets in .NET in general.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        ///     Gets or sets the topic alias maximum.
        ///     This gives the maximum length of the topic alias.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public ushort TopicAliasMaximum { get; set; }

        /// <summary>
        ///     If set to true, the bridge will attempt to indicate to the remote broker that it is a bridge not an ordinary
        ///     client.
        ///     If successful, this means that loop detection will be more effective and that retained messages will be propagated
        ///     correctly.
        ///     <remarks>
        ///         Not all brokers support this feature so it may be necessary to set it to false if your bridge does not
        ///         connect properly.
        ///     </remarks>
        /// </summary>
        public bool TryPrivate { get; set; } = true;

        /// <summary>
        ///     Gets or sets the user properties.
        ///     In MQTT 5, user properties are basic UTF-8 string key-value pairs that you can append to almost every type of MQTT
        ///     packet.
        ///     As long as you don’t exceed the maximum message size, you can use an unlimited number of user properties to add
        ///     metadata to MQTT messages and pass information between publisher, broker, and subscriber.
        ///     The feature is very similar to the HTTP header concept.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public List<MqttUserProperty> UserProperties { get; set; } = [];

        /// <summary>
        ///     When this feature is enabled the client will check if used properties are supported in the selected protocol
        ///     version.
        ///     This feature can be validated if an application message is generated one time but sent via different protocol
        ///     versions.
        ///     Default values are applied if the validation is off and features are not supported.
        /// </summary>
        public bool ValidateFeatures { get; set; } = true;

        /// <summary>
        ///     Gets or sets the content type of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string? WillContentType { get; set; }

        /// <summary>
        ///     Gets or sets the correlation data of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public byte[]? WillCorrelationData { get; set; }

        /// <summary>
        ///     Gets or sets the will delay interval.
        ///     This is the time between the client disconnect and the time the will message will be sent.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint WillDelayInterval { get; set; }

        /// <summary>
        ///     Gets or sets the message expiry interval of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint WillMessageExpiryInterval { get; set; }

        /// <summary>
        ///     Gets or sets the payload of the will message.
        /// </summary>
        public byte[]? WillPayload { get; set; }

        /// <summary>
        ///     Gets or sets the payload format indicator of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttPayloadFormatIndicator WillPayloadFormatIndicator { get; set; } = MqttPayloadFormatIndicator.Unspecified;

        /// <summary>
        ///     Gets or sets the QoS level of the will message.
        /// </summary>
        public MqttQualityOfServiceLevel WillQualityOfServiceLevel { get; set; }

        /// <summary>
        ///     Gets or sets the response topic of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string? WillResponseTopic { get; set; }

        /// <summary>
        ///     Gets or sets the retain flag of the will message.
        /// </summary>
        public bool WillRetain { get; set; }

        /// <summary>
        ///     Gets or sets the topic of the will message.
        /// </summary>
        public string? WillTopic { get; set; }

        /// <summary>
        ///     Gets or sets the user properties of the will message.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public List<MqttUserProperty> WillUserProperties { get; set; } = [];

        /// <summary>
        ///     Gets or sets the default and initial size of the packet write buffer.
        ///     It is recommended to set this to a value close to the usual expected packet size * 1.5.
        ///     Do not change this value when no memory issues are experienced.
        /// </summary>
        public int WriterBufferSize { get; set; } = 4096;

        /// <summary>
        ///     Gets or sets the maximum size of the buffer writer. The writer will reduce its internal buffer
        ///     to this value after serializing a packet.
        ///     Do not change this value when no memory issues are experienced.
        /// </summary>
        public int WriterBufferSizeMax { get; set; } = 65535;

        public void AddUserProperty(string name, string value)
        {
            UserProperties.Add(new MqttUserProperty(name, value));
        }
    }
}