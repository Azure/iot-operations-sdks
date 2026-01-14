// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::num::{NonZeroU16, NonZeroU32};

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};

use crate::azure_mqtt::mqtt_proto::{
    Authentication, ByteStr, DecodeError, EncodeError, KeepAlive, PacketMeta, Property,
    PropertyRef, ProtocolVersion, QoS, SharedExt as _, UserProperties,
    property::SessionExpiryInterval,
};

/// Ref: 3.2 CONNACK – Acknowledge connection request
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct ConnAck<S>
where
    S: Shared,
{
    pub reason_code: ConnectReasonCode,
    pub other_properties: ConnAckOtherProperties<S>,
}

impl<S> ConnAck<S>
where
    S: Shared,
{
    pub fn is_success(&self) -> bool {
        matches!(self.reason_code, ConnectReasonCode::Success { .. })
    }
}

// clippy thinks this should be a state machine enum or that the bools should be enums, which is nonsense
#[expect(clippy::struct_excessive_bools)]
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct ConnAckOtherProperties<S>
where
    S: Shared,
{
    pub session_expiry_interval: Option<SessionExpiryInterval>,
    pub receive_maximum: NonZeroU16,
    pub maximum_qos: QoS,
    pub retain_available: bool,
    pub maximum_packet_size: NonZeroU32,
    pub assigned_client_id: Option<ByteStr<S>>,
    pub topic_alias_maximum: u16,
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
    pub wildcard_subscription_available: bool,
    pub shared_subscription_available: bool,
    pub subscription_identifiers_available: bool,
    pub server_keep_alive: Option<KeepAlive>,
    pub response_information: Option<ByteStr<S>>,
    pub server_reference: Option<ByteStr<S>>,
    pub authentication: Option<Authentication<S>>,
}

/// Ref: 3.2.2.2 Connect Reason Code
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ConnectReasonCode {
    Success { session_present: bool },
    Refused(ConnectionRefusedReason),
}

define_u8_code! {
    /// Ref: 3.2.2.2 Connect Reason Code
    enum ConnectionRefusedReason,
    UnrecognizedConnectReasonCode,
    UnspecifiedError = 0x80,
    MalformedPacket = 0x81,
    ProtocolError = 0x82,
    ImplementationSpecificError = 0x83,
    UnsupportedProtocolVersion = 0x84,
    ClientIdentifierNotValid = 0x85,
    BadUserNameOrPassword = 0x86,
    NotAuthorized = 0x87,
    ServerUnavailable = 0x88,
    ServerBusy = 0x89,
    Banned = 0x8A,
    BadAuthenticationMethod = 0x8C,
    TopicNameInvalid = 0x90,
    PacketTooLarge = 0x95,
    QuotaExceeded = 0x97,
    PayloadFormatInvalid = 0x99,
    RetainNotSupported = 0x9A,
    QoSNotSupported = 0x9B,
    UseAnotherServer = 0x9C,
    ServerMoved = 0x9D,
    ConnectionRateExceeded = 0x9F,
}

impl<S> ConnAck<S>
where
    S: Shared,
{
    /// Creates a copy of this `ConnAck` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<ConnAck<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(ConnAck {
            reason_code: self.reason_code,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> PacketMeta<S> for ConnAck<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x20;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let connack_flags = src.try_get_u8()?;
        let session_present = match connack_flags {
            0x00 => false,
            0x01 => true,
            connack_flags => {
                return Err(DecodeError::UnrecognizedConnAckFlags(connack_flags));
            }
        };

        let reason_code = ConnectReasonCode::try_from(src.try_get_u8()?, session_present, version)?;

        let other_properties = match version {
            ProtocolVersion::V3 => Default::default(),

            ProtocolVersion::V5 => {
                decode_properties!(
                    src,
                    session_expiry_interval: SessionExpiryInterval,
                    receive_maximum: ReceiveMaximum,
                    maximum_qos: MaximumQoS,
                    retain_available: RetainAvailable,
                    maximum_packet_size: MaximumPacketSize,
                    assigned_client_id: AssignedClientIdentifier,
                    topic_alias_maximum: TopicAliasMaximum,
                    reason_string: ReasonString,
                    user_properties: Vec<UserProperty>,
                    wildcard_subscription_available: WildcardSubscriptionAvailable,
                    shared_subscription_available: SharedSubscriptionAvailable,
                    subscription_identifiers_available: SubscriptionIdentifiersAvailable,
                    server_keep_alive: ServerKeepAlive,
                    response_information: ResponseInformation,
                    server_reference: ServerReference,
                    authentication_method: AuthenticationMethod,
                    authentication_data: AuthenticationData,
                );

                ConnAckOtherProperties {
                    session_expiry_interval,
                    receive_maximum: receive_maximum.unwrap_or(NonZeroU16::MAX),
                    maximum_qos: maximum_qos.unwrap_or(QoS::ExactlyOnce),
                    retain_available: retain_available.unwrap_or(true),
                    maximum_packet_size: maximum_packet_size.unwrap_or(NonZeroU32::MAX),
                    assigned_client_id,
                    topic_alias_maximum: topic_alias_maximum.unwrap_or(0),
                    reason_string,
                    user_properties,
                    wildcard_subscription_available: wildcard_subscription_available
                        .unwrap_or(true),
                    shared_subscription_available: shared_subscription_available.unwrap_or(true),
                    subscription_identifiers_available: subscription_identifiers_available
                        .unwrap_or(true),
                    server_keep_alive,
                    response_information,
                    server_reference,
                    authentication: Authentication::of(authentication_method, authentication_data)?,
                }
            }
        };

        Ok(Self {
            reason_code,
            other_properties,
        })
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            reason_code,
            other_properties,
        } = self;

        let session_present = if let ConnectReasonCode::Success { session_present } = reason_code {
            *session_present
        } else {
            false
        };
        if session_present {
            dst.try_put_u8(0x01)
                .ok_or(EncodeError::InsufficientBuffer)?;
        } else {
            dst.try_put_u8(0x00)
                .ok_or(EncodeError::InsufficientBuffer)?;
        }

        dst.try_put_u8(reason_code.to_u8(version))
            .ok_or(EncodeError::InsufficientBuffer)?;

        if version.is_v5() {
            let ConnAckOtherProperties {
                session_expiry_interval,
                receive_maximum,
                maximum_qos,
                retain_available,
                maximum_packet_size,
                assigned_client_id,
                topic_alias_maximum,
                reason_string,
                user_properties,
                wildcard_subscription_available,
                shared_subscription_available,
                subscription_identifiers_available,
                server_keep_alive,
                response_information,
                server_reference,
                authentication,
            } = other_properties;

            let (authentication_method, authentication_data) =
                Authentication::into_parts(authentication.as_ref());

            encode_properties! {
                dst,
                session_expiry_interval: Option<SessionExpiryInterval>,
                receive_maximum: ReceiveMaximum,
                maximum_qos: MaximumQoS,
                retain_available: RetainAvailable,
                maximum_packet_size: MaximumPacketSize,
                assigned_client_id: Option<AssignedClientIdentifier>,
                topic_alias_maximum: TopicAliasMaximum,
                reason_string: Option<ReasonString>,
                user_properties: Vec<UserProperty>,
                wildcard_subscription_available: WildcardSubscriptionAvailable,
                shared_subscription_available: SharedSubscriptionAvailable,
                subscription_identifiers_available: SubscriptionIdentifiersAvailable,
                server_keep_alive: Option<ServerKeepAlive>,
                response_information: Option<ResponseInformation>,
                server_reference: Option<ServerReference>,
                authentication_method: Option<AuthenticationMethod>,
                authentication_data: Option<AuthenticationData>,
            }
        }

        Ok(())
    }
}

impl<S> ConnAckOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `ConnAckOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<ConnAckOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let assigned_client_id = if let Some(assigned_client_id) = &self.assigned_client_id {
            Some(assigned_client_id.to_shared(owned)?)
        } else {
            None
        };

        let reason_string = if let Some(reason_string) = &self.reason_string {
            Some(reason_string.to_shared(owned)?)
        } else {
            None
        };

        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        let response_information = if let Some(response_information) = &self.response_information {
            Some(response_information.to_shared(owned)?)
        } else {
            None
        };

        let server_reference = if let Some(server_reference) = &self.server_reference {
            Some(server_reference.to_shared(owned)?)
        } else {
            None
        };

        let authentication = if let Some(authentication) = &self.authentication {
            Some(authentication.to_shared(owned)?)
        } else {
            None
        };

        Ok(ConnAckOtherProperties {
            session_expiry_interval: self.session_expiry_interval,
            receive_maximum: self.receive_maximum,
            maximum_qos: self.maximum_qos,
            retain_available: self.retain_available,
            maximum_packet_size: self.maximum_packet_size,
            assigned_client_id,
            topic_alias_maximum: self.topic_alias_maximum,
            reason_string,
            user_properties,
            wildcard_subscription_available: self.wildcard_subscription_available,
            shared_subscription_available: self.shared_subscription_available,
            subscription_identifiers_available: self.subscription_identifiers_available,
            server_keep_alive: self.server_keep_alive,
            response_information,
            server_reference,
            authentication,
        })
    }
}

/// Ref: 3.2.2.3 Connect Return code
mod v3_refused_reason_codes {
    pub(super) const UNACCEPTABLE_PROTOCOL_VERSION: u8 = 0x01;
    pub(super) const IDENTIFIER_REJECTED: u8 = 0x02;
    pub(super) const SERVER_UNAVAILABLE: u8 = 0x03;
    pub(super) const BAD_USER_NAME_OR_PASSWORD: u8 = 0x04;
    pub(super) const NOT_AUTHORIZED: u8 = 0x05;
}

impl ConnectReasonCode {
    fn try_from(
        code: u8,
        session_present: bool,
        version: ProtocolVersion,
    ) -> Result<Self, DecodeError> {
        Ok(match (version, code) {
            (_, 0x00) => Self::Success { session_present },

            (ProtocolVersion::V3, v3_refused_reason_codes::UNACCEPTABLE_PROTOCOL_VERSION) => {
                Self::Refused(ConnectionRefusedReason::UnsupportedProtocolVersion)
            }

            (ProtocolVersion::V3, v3_refused_reason_codes::IDENTIFIER_REJECTED) => {
                Self::Refused(ConnectionRefusedReason::ClientIdentifierNotValid)
            }

            (ProtocolVersion::V3, v3_refused_reason_codes::SERVER_UNAVAILABLE) => {
                Self::Refused(ConnectionRefusedReason::ServerUnavailable)
            }

            (ProtocolVersion::V3, v3_refused_reason_codes::BAD_USER_NAME_OR_PASSWORD) => {
                Self::Refused(ConnectionRefusedReason::BadUserNameOrPassword)
            }

            (ProtocolVersion::V3, v3_refused_reason_codes::NOT_AUTHORIZED) => {
                Self::Refused(ConnectionRefusedReason::NotAuthorized)
            }

            (ProtocolVersion::V3, code) => {
                return Err(DecodeError::UnrecognizedConnectReturnCode(code));
            }

            (ProtocolVersion::V5, code) => Self::Refused(code.try_into()?),
        })
    }

    fn to_u8(self, version: ProtocolVersion) -> u8 {
        match (version, self) {
            (_, Self::Success { .. }) => 0x00,

            (
                ProtocolVersion::V3,
                Self::Refused(ConnectionRefusedReason::UnsupportedProtocolVersion),
            ) => v3_refused_reason_codes::UNACCEPTABLE_PROTOCOL_VERSION,

            (
                ProtocolVersion::V3,
                Self::Refused(ConnectionRefusedReason::ClientIdentifierNotValid),
            ) => v3_refused_reason_codes::IDENTIFIER_REJECTED,

            (
                ProtocolVersion::V3,
                Self::Refused(ConnectionRefusedReason::BadUserNameOrPassword),
            ) => v3_refused_reason_codes::BAD_USER_NAME_OR_PASSWORD,

            (ProtocolVersion::V3, Self::Refused(ConnectionRefusedReason::NotAuthorized)) => {
                v3_refused_reason_codes::NOT_AUTHORIZED
            }

            (ProtocolVersion::V3, Self::Refused(ConnectionRefusedReason::ServerUnavailable)) => {
                v3_refused_reason_codes::SERVER_UNAVAILABLE
            }

            (ProtocolVersion::V3, Self::Refused(_)) => v3_refused_reason_codes::SERVER_UNAVAILABLE,

            (ProtocolVersion::V5, Self::Refused(reason)) => reason.into(),
        }
    }
}

impl<S> Default for ConnAckOtherProperties<S>
where
    S: Shared,
{
    fn default() -> Self {
        Self {
            session_expiry_interval: None,
            receive_maximum: NonZeroU16::MAX,
            maximum_qos: QoS::ExactlyOnce,
            retain_available: true,
            maximum_packet_size: NonZeroU32::MAX,
            assigned_client_id: None,
            topic_alias_maximum: 0,
            reason_string: None,
            user_properties: vec![],
            wildcard_subscription_available: true,
            shared_subscription_available: true,
            subscription_identifiers_available: true,
            server_keep_alive: None,
            response_information: None,
            server_reference: None,
            authentication: None,
        }
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use super::*;
    use crate::azure_mqtt::mqtt_proto::{self, Packet};

    encode_decode_v3! {
        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: false,
            },
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Refused(
                ConnectionRefusedReason::UnsupportedProtocolVersion
            ),
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Refused(
                ConnectionRefusedReason::ClientIdentifierNotValid
            ),
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Refused(
                ConnectionRefusedReason::ServerUnavailable
            ),
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Refused(
                ConnectionRefusedReason::BadUserNameOrPassword
            ),
            other_properties: Default::default(),
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Refused(
                ConnectionRefusedReason::NotAuthorized
            ),
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: Some(SessionExpiryInterval::Infinite),
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: false,
                shared_subscription_available: false,
                subscription_identifiers_available: false,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::AtMostOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(1).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(1).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 10,
                reason_string: None,
                user_properties: vec![],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),

        Packet::ConnAck(ConnAck {
            reason_code: ConnectReasonCode::Success {
                session_present: true,
            },
            other_properties: ConnAckOtherProperties {
                session_expiry_interval: None,
                receive_maximum: NonZeroU16::new(u16::MAX).unwrap(),
                maximum_qos: QoS::ExactlyOnce,
                retain_available: true,
                maximum_packet_size: NonZeroU32::new(u32::MAX).unwrap(),
                assigned_client_id: None,
                topic_alias_maximum: 0,
                reason_string: None,
                user_properties: vec![("key".into(), "value".into())],
                wildcard_subscription_available: true,
                shared_subscription_available: true,
                subscription_identifiers_available: true,
                server_keep_alive: None,
                response_information: None,
                server_reference: None,
                authentication: None,
            },
        }),
    }

    #[test]
    fn encode_decode_all_the_codes() {
        for code in enum_iterator::all::<ConnectionRefusedReason>() {
            let packet = Packet::ConnAck(ConnAck {
                reason_code: ConnectReasonCode::Refused(code),
                other_properties: Default::default(),
            });

            let mut encoding = mqtt_proto::tests::encode(&packet, mqtt_proto::ProtocolVersion::V5);
            println!("encoded packet: {encoding:?}");
            let decoded_packet =
                mqtt_proto::tests::decode(&mut encoding, mqtt_proto::ProtocolVersion::V5);

            assert_eq!(packet, decoded_packet);
        }
    }

    #[test]
    fn zero_session_expiry_interval() {
        let mut src = mqtt_proto::tests::create_packet_as_shared(
            0x20,
            &[0x00, 0x00, 0x05, 0x11, 0x00, 0x00, 0x00, 0x00],
        );
        let packet = mqtt_proto::tests::decode(&mut src, ProtocolVersion::V5);

        assert_matches!(
            &packet,
            Packet::ConnAck(ConnAck {
                reason_code: ConnectReasonCode::Success {
                    session_present: false,
                },
                other_properties: ConnAckOtherProperties {
                    session_expiry_interval: Some(SessionExpiryInterval::Duration(0)),
                    ..
                },
            })
        );

        let encoded_packet = mqtt_proto::tests::encode(&packet, ProtocolVersion::V5);
        assert_eq!(
            encoded_packet.as_ref(),
            &[0x20, 0x08, 0x00, 0x00, 0x05, 0x11, 0x00, 0x00, 0x00, 0x00]
        );
    }
}
