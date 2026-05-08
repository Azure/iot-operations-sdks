// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, PacketIdentifier, PacketMeta, Property, PropertyRef,
    ProtocolVersion, SharedExt as _, UserProperties,
};

/// Ref: 3.11 UNSUBACK – Unsubscribe acknowledgement
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct UnsubAck<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub reason_codes: Vec<UnsubAckReasonCode>,
    pub other_properties: UnsubAckOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct UnsubAckOtherProperties<S>
where
    S: Shared,
{
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
}

define_u8_code! {
    /// Ref: 3.11.3 UNSUBACK Payload
    enum UnsubAckReasonCode,
    UnrecognizedUnsubAckReasonCode,
    Success = 0x00,
    NoSubscriptionExisted = 0x11,
    UnspecifiedError = 0x80,
    ImplementationSpecificError = 0x83,
    NotAuthorized = 0x87,
    TopicFilterInvalid = 0x8F,
    PacketIdentifierInUse = 0x91,
}

impl<S> UnsubAck<S>
where
    S: Shared,
{
    /// Creates a copy of this `UnsubAck` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<UnsubAck<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(UnsubAck {
            packet_identifier: self.packet_identifier,
            reason_codes: self.reason_codes.clone(),
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> PacketMeta<S> for UnsubAck<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0xB0;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        let (reason_codes, other_properties) = match version {
            ProtocolVersion::V3 => Default::default(),

            ProtocolVersion::V5 => {
                decode_properties!(
                    src,
                    reason_string: ReasonString,
                    user_properties: Vec<UserProperty>,
                );

                let reason_codes: Result<Vec<_>, _> = src
                    .as_ref()
                    .iter()
                    .map(|&reason_code| reason_code.try_into())
                    .collect();
                let reason_codes = reason_codes?;
                src.drain(reason_codes.len());

                if reason_codes.is_empty() {
                    return Err(DecodeError::NoTopics);
                }

                (
                    reason_codes,
                    UnsubAckOtherProperties {
                        reason_string,
                        user_properties,
                    },
                )
            }
        };

        Ok(Self {
            packet_identifier,
            reason_codes,
            other_properties,
        })
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            packet_identifier,
            reason_codes,
            other_properties:
                UnsubAckOtherProperties {
                    reason_string,
                    user_properties,
                },
        } = self;

        dst.try_put_u16_be(packet_identifier.0.get())
            .ok_or(EncodeError::InsufficientBuffer)?;

        match version {
            ProtocolVersion::V3 => {
                if let Some(&reason_code) = reason_codes
                    .iter()
                    .find(|reason_code| !reason_code.is_success())
                {
                    return Err(EncodeError::NegativeUnsubAck(reason_code));
                }
            }

            ProtocolVersion::V5 => {
                encode_properties! {
                    dst,
                    reason_string: Option<ReasonString>,
                    user_properties: Vec<UserProperty>,
                }

                for &reason_code in reason_codes {
                    dst.try_put_u8(reason_code.into())
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }
        }

        Ok(())
    }
}

impl<S> UnsubAckOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `UnsubAckOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<UnsubAckOtherProperties<O2::Shared>, buffer_pool::Error>
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

        Ok(UnsubAckOtherProperties {
            reason_string,
            user_properties,
        })
    }
}

impl UnsubAckReasonCode {
    pub fn is_success(self) -> bool {
        matches!(self, Self::Success | Self::NoSubscriptionExisted)
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use test_case::test_case;

    use buffer_pool::Shared;

    use super::*;
    use crate::azure_mqtt::mqtt_proto::{self, BinaryData, Packet};

    encode_decode_v3! {
        Packet::UnsubAck(UnsubAck {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            reason_codes: vec![],
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::UnsubAck(UnsubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_codes: vec![
                UnsubAckReasonCode::Success,
                UnsubAckReasonCode::NoSubscriptionExisted,
                UnsubAckReasonCode::UnspecifiedError,
                UnsubAckReasonCode::ImplementationSpecificError,
                UnsubAckReasonCode::NotAuthorized,
                UnsubAckReasonCode::TopicFilterInvalid,
                UnsubAckReasonCode::PacketIdentifierInUse,
            ],
            other_properties: UnsubAckOtherProperties {
                reason_string: Some("reason".into()),
                user_properties: vec![("foo".into(), "bar".into())],
            },
        }),
    }

    #[test_case(b"\xb0\x0e\x12\x34\x0a\x1f\x00\x07Success\x00", UnsubAckReasonCode::Success, "Success", ProtocolVersion::V5; "Decode Unsuback Success")]
    #[test_case(b"\xb0\x1e\x12\x34\x1a\x1f\x00\x17No subscription existed\x11", UnsubAckReasonCode::NoSubscriptionExisted,"No subscription existed",  ProtocolVersion::V5; "Decode Unsuback NoSubscriptionExisted")]
    #[test_case(b"\xb0\x18\x12\x34\x14\x1f\x00\x11Unspecified error\x80", UnsubAckReasonCode::UnspecifiedError,"Unspecified error", ProtocolVersion::V5; "Decode Unsuback UnspecifiedError")]
    #[test_case(b"\xb0\x24\x12\x34\x20\x1f\x00\x1dImplementation specific error\x83", UnsubAckReasonCode::ImplementationSpecificError,"Implementation specific error", ProtocolVersion::V5; "Decode Unsuback ImplementationSpecificError")]
    #[test_case(b"\xb0\x15\x12\x34\x11\x1f\x00\x0eNot authorized\x87", UnsubAckReasonCode::NotAuthorized,"Not authorized", ProtocolVersion::V5; "Decode Unsuback NotAuthorized")]
    #[test_case(b"\xb0\x1b\x12\x34\x17\x1f\x00\x14Topic Filter invalid\x8f", UnsubAckReasonCode::TopicFilterInvalid, "Topic Filter invalid", ProtocolVersion::V5; "Decode Unsuback TopicFilterInvalid")]
    #[test_case(b"\xb0\x1f\x12\x34\x1b\x1f\x00\x18Packet Identifier in use\x91", UnsubAckReasonCode::PacketIdentifierInUse,"Packet Identifier in use", ProtocolVersion::V5; "Decode Unsuback PacketIdentifierInUse")]
    // Lint wants `encoding` to be taken as borrow, but that makes the `test_case()` exprs more complicated.
    #[allow(clippy::needless_pass_by_value)]
    fn decode_packet_unsuback_v5(
        encoding: &[u8],
        reason: UnsubAckReasonCode,
        reason_str: &str,
        version: ProtocolVersion,
    ) {
        let encoding = BinaryData::<Bytes>::from(encoding);
        let mut buffer = encoding.clone().into_shared();
        buffer.drain(std::mem::size_of::<u16>());

        let packet = Packet::UnsubAck(UnsubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_codes: vec![reason],
            other_properties: UnsubAckOtherProperties {
                reason_string: Some(reason_str.into()),
                user_properties: vec![],
            },
        });

        let new_encoding = mqtt_proto::tests::try_encode(&packet, version);

        assert_eq!(new_encoding.unwrap().as_ref(), encoding.as_ref());
    }
}
