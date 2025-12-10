// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, PacketIdentifier, PacketMeta, Property, PropertyRef,
    ProtocolVersion, SharedExt as _, UserProperties,
};

/// Ref: 3.5 PUBREC – Publish received (QoS 2 publish received, part 1)
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct PubRec<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub reason_code: PubRecReasonCode,
    pub other_properties: PubRecOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct PubRecOtherProperties<S>
where
    S: Shared,
{
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
}

define_u8_code! {
    /// Ref: 3.5.2.1 PUBREC Reason Code
    enum PubRecReasonCode,
    UnrecognizedPubRecReasonCode,
    Success = 0x00,
    NoMatchingSubscribers = 0x10,
    UnspecifiedError = 0x80,
    ImplementationSpecificError = 0x83,
    NotAuthorized = 0x87,
    TopicNameInvalid = 0x90,
    PacketIdentifierInUse = 0x91,
    QuotaExceeded = 0x97,
    PayloadFormatInvalid = 0x99,
}

impl<S> PacketMeta<S> for PubRec<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x50;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        match version {
            ProtocolVersion::V3 => Ok(Self {
                packet_identifier,
                reason_code: PubRecReasonCode::Success,
                other_properties: Default::default(),
            }),

            ProtocolVersion::V5 => match src.try_get_u8() {
                Ok(reason_code) => {
                    let reason_code = reason_code.try_into()?;

                    if src.is_empty() {
                        // See comment in `PubAck::decode` for explanation.

                        Ok(Self {
                            packet_identifier,
                            reason_code,
                            other_properties: Default::default(),
                        })
                    } else {
                        decode_properties!(
                            src,
                            reason_string: ReasonString,
                            user_properties: Vec<UserProperty>,
                        );

                        Ok(Self {
                            packet_identifier,
                            reason_code,
                            other_properties: PubRecOtherProperties {
                                reason_string,
                                user_properties,
                            },
                        })
                    }
                }

                Err(DecodeError::IncompletePacket) => Ok(Self {
                    packet_identifier,
                    reason_code: PubRecReasonCode::Success,
                    other_properties: Default::default(),
                }),

                Err(err) => Err(err),
            },
        }
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            packet_identifier,
            reason_code,
            other_properties,
        } = self;

        dst.try_put_u16_be(packet_identifier.0.get())
            .ok_or(EncodeError::InsufficientBuffer)?;

        match version {
            ProtocolVersion::V3 => {
                if !reason_code.is_success() {
                    return Err(EncodeError::NegativePubRec(*reason_code));
                }
            }

            ProtocolVersion::V5 => {
                let PubRecOtherProperties {
                    reason_string,
                    user_properties,
                } = other_properties;

                let default_reason_code = (*reason_code) == PubRecReasonCode::Success;
                let default_property_length = reason_string.is_none() && user_properties.is_empty();

                if !default_reason_code || !default_property_length {
                    dst.try_put_u8((*reason_code).into())
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }

                if !default_property_length {
                    encode_properties! {
                        dst,
                        reason_string: Option<ReasonString>,
                        user_properties: Vec<UserProperty>,
                    }
                }
            }
        }

        Ok(())
    }
}

impl PubRecReasonCode {
    pub fn is_success(self) -> bool {
        matches!(self, Self::Success | Self::NoMatchingSubscribers)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::azure_mqtt::mqtt_proto::Packet;

    encode_decode_v3! {
        Packet::PubRec(PubRec {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubRecReasonCode::Success,
            other_properties: Default::default(),
        }) => b"\x50\x02\x12\x34",
    }

    encode_decode_v5! {
        Packet::PubRec(PubRec {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubRecReasonCode::Success,
            other_properties: Default::default(),
        }) => b"\x50\x02\x12\x34",

        Packet::PubRec(PubRec {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubRecReasonCode::Success,
            other_properties: PubRecOtherProperties {
                reason_string: Some("foo".into()),
                user_properties: vec![("bar".into(), "baz".into())],
            },
        }) => b"\x50\x15\x12\x34\x00\x11\x1f\x00\x03foo\x26\x00\x03bar\x00\x03baz",

        Packet::PubRec(PubRec {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubRecReasonCode::NoMatchingSubscribers,
            other_properties: Default::default(),
        }) => b"\x50\x03\x12\x34\x10",

        Packet::PubRec(PubRec {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubRecReasonCode::NoMatchingSubscribers,
            other_properties: PubRecOtherProperties {
                reason_string: Some("foo".into()),
                user_properties: vec![("bar".into(), "baz".into())],
            },
        }) => b"\x50\x15\x12\x34\x10\x11\x1f\x00\x03foo\x26\x00\x03bar\x00\x03baz",
    }
}
