// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, PacketIdentifier, PacketMeta, Property, PropertyRef,
    ProtocolVersion, SharedExt as _, UserProperties,
};

/// Ref: 3.4 PUBACK – Publish acknowledgement
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct PubAck<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub reason_code: PubAckReasonCode,
    pub other_properties: PubAckOtherProperties<S>,
}

impl<S> PubAck<S>
where
    S: Shared,
{
    /// Creates a copy of this `PubAck` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<PubAck<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(PubAck {
            packet_identifier: self.packet_identifier,
            reason_code: self.reason_code,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct PubAckOtherProperties<S>
where
    S: Shared,
{
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
}

impl<S> PubAckOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `PubAckOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<PubAckOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let reason_string = match &self.reason_string {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        Ok(PubAckOtherProperties {
            reason_string,
            user_properties,
        })
    }
}

define_u8_code! {
    /// Ref: 3.4.2.1 PUBACK Reason Code
    enum PubAckReasonCode,
    UnrecognizedPubAckReasonCode,
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

impl<S> PacketMeta<S> for PubAck<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x40;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        match version {
            ProtocolVersion::V3 => Ok(Self {
                packet_identifier,
                reason_code: PubAckReasonCode::Success,
                other_properties: Default::default(),
            }),

            ProtocolVersion::V5 => match src.try_get_u8() {
                Ok(reason_code) => {
                    let reason_code: PubAckReasonCode = reason_code.try_into()?;

                    if src.is_empty() {
                        // The spec allows the reason code and property length to be omitted if the reason code is `Success` and
                        // there are no properties, which is handled by the `Err(DecodeError::IncompletePacket)` arm below.
                        //
                        // >The Reason Code and Property Length can be omitted if the Reason Code is 0x00 (Success) and there are no Properties.
                        // > In this case the PUBACK has a Remaining Length of 2.
                        //
                        // Furthermore, the spec allows the property length to be omitted if there are no properties, regardless of the reason code.
                        //
                        // >If the Remaining Length is less than 4 there is no Property Length and the value of 0 is used.
                        //
                        // This is also true of PUBCOMP, PUBREC and PUBREL.

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
                            other_properties: PubAckOtherProperties {
                                reason_string,
                                user_properties,
                            },
                        })
                    }
                }

                Err(DecodeError::IncompletePacket) => Ok(Self {
                    packet_identifier,
                    reason_code: PubAckReasonCode::Success,
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
                    return Err(EncodeError::NegativePubAck(*reason_code));
                }
            }

            ProtocolVersion::V5 => {
                let PubAckOtherProperties {
                    reason_string,
                    user_properties,
                } = other_properties;

                let default_reason_code = (*reason_code) == PubAckReasonCode::Success;
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

impl PubAckReasonCode {
    pub fn is_success(self) -> bool {
        matches!(self, Self::Success | Self::NoMatchingSubscribers)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::azure_mqtt::mqtt_proto::Packet;

    encode_decode_v3! {
        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::Success,
            other_properties: Default::default(),
        }) => b"\x40\x02\x12\x34",
    }

    encode_decode_v5! {
        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::Success,
            other_properties: Default::default(),
        }) => b"\x40\x02\x12\x34",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::Success,
            other_properties: PubAckOtherProperties {
                reason_string: Some("foo".into()),
                user_properties: vec![("bar".into(), "baz".into())],
            },
        }) => b"\x40\x15\x12\x34\x00\x11\x1f\x00\x03foo\x26\x00\x03bar\x00\x03baz",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::NoMatchingSubscribers,
            other_properties: Default::default(),
        }) => b"\x40\x03\x12\x34\x10",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::NoMatchingSubscribers,
            other_properties: PubAckOtherProperties {
                reason_string: Some("foo".into()),
                user_properties: vec![("bar".into(), "baz".into())],
            },
        }) => b"\x40\x15\x12\x34\x10\x11\x1f\x00\x03foo\x26\x00\x03bar\x00\x03baz",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::UnspecifiedError,
            other_properties: Default::default(),
        }) => b"\x40\x03\x12\x34\x80",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::UnspecifiedError,
            other_properties: PubAckOtherProperties {
                reason_string: Some("foo".into()),
                user_properties: vec![("bar".into(), "baz".into())],
            },
        }) => b"\x40\x15\x12\x34\x80\x11\x1f\x00\x03foo\x26\x00\x03bar\x00\x03baz",

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::ImplementationSpecificError,
            other_properties: Default::default(),
        }),

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::NotAuthorized,
            other_properties: Default::default(),
        }),

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::TopicNameInvalid,
            other_properties: Default::default(),
        }),

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::PacketIdentifierInUse,
            other_properties: Default::default(),
        }),

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::QuotaExceeded,
            other_properties: Default::default(),
        }),

        Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_code: PubAckReasonCode::PayloadFormatInvalid,
            other_properties: Default::default(),
        }),
    }
}
