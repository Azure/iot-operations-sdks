// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, PacketMeta, Property, PropertyRef, ProtocolVersion,
    SharedExt as _, UserProperties, property::SessionExpiryInterval,
};

/// Ref: 3.14 DISCONNECT - Disconnect notification
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct Disconnect<S>
where
    S: Shared,
{
    pub reason_code: DisconnectReasonCode,
    pub other_properties: DisconnectOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct DisconnectOtherProperties<S>
where
    S: Shared,
{
    pub session_expiry_interval: Option<SessionExpiryInterval>,
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
    pub server_reference: Option<ByteStr<S>>,
}

define_u8_code! {
    /// Ref: 3.14.2.1 Disconnect Reason Code
    enum DisconnectReasonCode,
    UnrecognizedDisconnectReasonCode,
    Normal = 0x00,
    DisconnectWithWillMessage = 0x04,
    UnspecifiedError = 0x80,
    MalformedPacket = 0x81,
    ProtocolError = 0x82,
    ImplementationSpecificError = 0x83,
    NotAuthorized = 0x87,
    ServerBusy = 0x89,
    ServerShuttingDown = 0x8B,
    KeepAliveTimeout = 0x8D,
    SessionTakenOver = 0x8E,
    TopicFilterInvalid = 0x8F,
    TopicNameInvalid = 0x90,
    ReceiveMaximumExceeded = 0x93,
    TopicAliasInvalid = 0x94,
    PacketTooLarge = 0x95,
    MessageRateTooHigh = 0x96,
    QuotaExceeded = 0x97,
    AdministrativeAction = 0x98,
    PayloadFormatInvalid = 0x99,
    RetainNotSupported = 0x9A,
    QosNotSupported = 0x9B,
    UseAnotherServer = 0x9C,
    ServerMoved = 0x9D,
    SharedSubscriptionsNotSupported = 0x9E,
    ConnectionRateExceeded = 0x9F,
    MaximumConnectTime = 0xA0,
    SubscriptionIdentifiersNotSupported = 0xA1,
    WildcardSubscriptionsNotSupported = 0xA2,
}

impl<S> Disconnect<S>
where
    S: Shared,
{
    pub fn new(reason_code: DisconnectReasonCode) -> Self {
        Self {
            reason_code,
            other_properties: Default::default(),
        }
    }

    /// Creates a copy of this `Disconnect` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Disconnect<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(Disconnect {
            reason_code: self.reason_code,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> From<DisconnectReasonCode> for Disconnect<S>
where
    S: Shared,
{
    fn from(reason: DisconnectReasonCode) -> Self {
        Self::new(reason)
    }
}

impl<S> PacketMeta<S> for Disconnect<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0xE0;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        match version {
            ProtocolVersion::V3 => Ok(Self {
                reason_code: DisconnectReasonCode::Normal,
                other_properties: Default::default(),
            }),

            ProtocolVersion::V5 => match src.try_get_u8() {
                Ok(reason_code) => {
                    let reason_code = reason_code.try_into()?;

                    // Specially for DISCONNECT packets, the spec allows clients to omit the properties length
                    // if there are no properties instead of needing to send a length of 0.
                    //
                    // >3.14.2.2.1 Property Length
                    // >
                    // >If the Remaining Length is less than 2, a value of 0 is used.
                    let other_properties = if src.len() >= 1 {
                        decode_properties!(
                            src,
                            session_expiry_interval: SessionExpiryInterval,
                            reason_string: ReasonString,
                            user_properties: Vec<UserProperty>,
                            server_reference: ServerReference,
                        );

                        DisconnectOtherProperties {
                            session_expiry_interval,
                            reason_string,
                            user_properties,
                            server_reference,
                        }
                    } else {
                        Default::default()
                    };

                    Ok(Self {
                        reason_code,
                        other_properties,
                    })
                }

                Err(DecodeError::IncompletePacket) => Ok(Self::new(DisconnectReasonCode::Normal)),

                Err(err) => Err(err),
            },
        }
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            reason_code,
            other_properties,
        } = self;

        if version.is_v5() {
            let DisconnectOtherProperties {
                session_expiry_interval,
                reason_string,
                user_properties,
                server_reference,
            } = other_properties;

            let need_variable_header = (*reason_code) != DisconnectReasonCode::Normal
                || session_expiry_interval.is_some()
                || reason_string.is_some()
                || !user_properties.is_empty()
                || server_reference.is_some();
            if need_variable_header {
                dst.try_put_u8((*reason_code).into())
                    .ok_or(EncodeError::InsufficientBuffer)?;

                if session_expiry_interval.is_some()
                    || reason_string.is_some()
                    || !user_properties.is_empty()
                    || server_reference.is_some()
                {
                    encode_properties! {
                        dst,
                        session_expiry_interval: Option<SessionExpiryInterval>,
                        reason_string: Option<ReasonString>,
                        user_properties: Vec<UserProperty>,
                        server_reference: Option<ServerReference>,
                    }
                }
            }
        }

        Ok(())
    }
}

impl<S> DisconnectOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `DisconnectOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<DisconnectOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
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

        let server_reference = if let Some(server_reference) = &self.server_reference {
            Some(server_reference.to_shared(owned)?)
        } else {
            None
        };

        Ok(DisconnectOtherProperties {
            session_expiry_interval: self.session_expiry_interval,
            reason_string,
            user_properties,
            server_reference,
        })
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use super::*;
    use crate::azure_mqtt::mqtt_proto::{self, Packet};

    encode_decode_v3! {
        Packet::Disconnect(Disconnect {
            reason_code: DisconnectReasonCode::Normal,
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::Disconnect(Disconnect {
            reason_code: DisconnectReasonCode::Normal,
            other_properties: DisconnectOtherProperties {
                session_expiry_interval: Some(SessionExpiryInterval::Duration(0)),
                reason_string: Some("just cause".into()),
                user_properties: vec![("key".into(), "value".into())],
                server_reference: Some("server".into()),
            },
        }),

        Packet::Disconnect(Disconnect {
            reason_code: DisconnectReasonCode::Normal,
            other_properties: DisconnectOtherProperties {
                session_expiry_interval: Some(SessionExpiryInterval::Duration(1)),
                reason_string: Some("just cause".into()),
                user_properties: vec![("key".into(), "value".into())],
                server_reference: Some("server".into()),
            },
        }),
    }

    #[test]
    fn encode_decode_all_the_codes() {
        for code in enum_iterator::all::<DisconnectReasonCode>() {
            let packet = Packet::Disconnect(Disconnect {
                reason_code: code,
                other_properties: DisconnectOtherProperties {
                    session_expiry_interval: Some(SessionExpiryInterval::Duration(1)),
                    reason_string: Some("just cause".into()),
                    user_properties: vec![("key".into(), "value".into())],
                    server_reference: Some("server".into()),
                },
            });

            let mut encoding = mqtt_proto::tests::encode(&packet, mqtt_proto::ProtocolVersion::V5);
            println!("encoded packet: {encoding:?}");
            let decoded_packet =
                mqtt_proto::tests::decode(&mut encoding, mqtt_proto::ProtocolVersion::V5);

            assert_eq!(packet, decoded_packet);
        }
    }

    #[test]
    fn no_properties_and_no_properties_length() {
        let mut src = mqtt_proto::tests::create_packet_as_shared(0xE0, &[0x04]);
        let packet = mqtt_proto::tests::decode(&mut src, ProtocolVersion::V5);

        assert_matches!(
            &packet,
            Packet::Disconnect(Disconnect {
                reason_code: DisconnectReasonCode::DisconnectWithWillMessage,
                ..
            })
        );

        let encoded_packet = mqtt_proto::tests::encode(&packet, ProtocolVersion::V5);
        assert_eq!(encoded_packet.as_ref(), &[0xE0, 0x01, 0x04]);
    }

    #[test]
    fn no_properties_and_explicit_properties_length() {
        let mut src = mqtt_proto::tests::create_packet_as_shared(0xE0, &[0x04, 0x00]);
        let packet = mqtt_proto::tests::decode(&mut src, ProtocolVersion::V5);
        assert_matches!(
            &packet,
            Packet::Disconnect(Disconnect {
                reason_code: DisconnectReasonCode::DisconnectWithWillMessage,
                ..
            })
        );

        let encoded_packet = mqtt_proto::tests::encode(&packet, ProtocolVersion::V5);
        assert_eq!(encoded_packet.as_ref(), &[0xE0, 0x01, 0x04]);
    }

    #[test]
    fn some_properties() {
        let mut src = mqtt_proto::tests::create_packet_as_shared(
            0xE0,
            &[0x04, 0x05, 0x11, 0xFF, 0xFF, 0xFF, 0xFF],
        );
        let packet = mqtt_proto::tests::decode(&mut src, ProtocolVersion::V5);

        assert_matches!(
            &packet,
            Packet::Disconnect(Disconnect {
                reason_code: DisconnectReasonCode::DisconnectWithWillMessage,
                other_properties: DisconnectOtherProperties {
                    session_expiry_interval: Some(SessionExpiryInterval::Infinite),
                    ..
                },
            })
        );

        let encoded_packet = mqtt_proto::tests::encode(&packet, ProtocolVersion::V5);
        assert_eq!(
            encoded_packet.as_ref(),
            &[0xE0, 0x07, 0x04, 0x05, 0x11, 0xFF, 0xFF, 0xFF, 0xFF]
        );
    }

    #[test]
    fn zero_session_expiry_interval() {
        let mut src = mqtt_proto::tests::create_packet_as_shared(
            0xE0,
            &[0x04, 0x05, 0x11, 0x00, 0x00, 0x00, 0x00],
        );
        let packet = mqtt_proto::tests::decode(&mut src, ProtocolVersion::V5);

        assert_matches!(
            &packet,
            Packet::Disconnect(Disconnect {
                reason_code: DisconnectReasonCode::DisconnectWithWillMessage,
                other_properties: DisconnectOtherProperties {
                    session_expiry_interval: Some(SessionExpiryInterval::Duration(0)),
                    ..
                },
            })
        );

        let encoded_packet = mqtt_proto::tests::encode(&packet, ProtocolVersion::V5);
        assert_eq!(
            encoded_packet.as_ref(),
            &[0xE0, 0x07, 0x04, 0x05, 0x11, 0x00, 0x00, 0x00, 0x00]
        );
    }
}
