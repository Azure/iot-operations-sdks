// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};

use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, PacketIdentifier, PacketMeta, Property, PropertyRef,
    ProtocolVersion, QoS, SharedExt as _, UserProperties,
};

/// Ref: 3.9 SUBACK – Subscribe acknowledgement
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct SubAck<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub reason_codes: Vec<SubscribeReasonCode>,
    pub other_properties: SubAckOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct SubAckOtherProperties<S>
where
    S: Shared,
{
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
}

define_u8_code! {
    /// Ref: 3.9.3 SUBACK Payload
    enum SubscribeReasonCode,
    UnrecognizedSubscribeReasonCode,
    GrantedQoS0 = 0x00,
    GrantedQoS1 = 0x01,
    GrantedQoS2 = 0x02,
    UnspecifiedError = 0x80,
    ImplementationSpecificError = 0x83,
    NotAuthorized = 0x87,
    TopicFilterInvalid = 0x8F,
    PacketIdentifierInUse = 0x91,
    QuotaExceeded = 0x97,
    SharedSubscriptionsNotSupported = 0x9E,
    SubscriptionIdentifiersNotSupported = 0xA1,
    WildcardSubscriptionsNotSupported = 0xA2,
}

impl SubscribeReasonCode {
    pub fn is_success(self) -> bool {
        matches!(
            self,
            SubscribeReasonCode::GrantedQoS0
                | SubscribeReasonCode::GrantedQoS1
                | SubscribeReasonCode::GrantedQoS2
        )
    }
}

impl<S> SubAck<S>
where
    S: Shared,
{
    /// Creates a copy of this `SubAck` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<SubAck<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(SubAck {
            packet_identifier: self.packet_identifier,
            reason_codes: self.reason_codes.clone(),
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> PacketMeta<S> for SubAck<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x90;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        let other_properties = match version {
            ProtocolVersion::V3 => Default::default(),

            ProtocolVersion::V5 => {
                decode_properties!(
                    src,
                    reason_string: ReasonString,
                    user_properties: Vec<UserProperty>,
                );

                SubAckOtherProperties {
                    reason_string,
                    user_properties,
                }
            }
        };

        let reason_codes: Result<Vec<_>, _> = src
            .as_ref()
            .iter()
            .map(|&reason_code| SubscribeReasonCode::try_from(reason_code, version))
            .collect();
        let reason_codes = reason_codes?;
        src.drain(reason_codes.len());

        if reason_codes.is_empty() {
            return Err(DecodeError::NoTopics);
        }

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
            other_properties,
        } = self;

        dst.try_put_u16_be(packet_identifier.0.get())
            .ok_or(EncodeError::InsufficientBuffer)?;

        if version.is_v5() {
            let SubAckOtherProperties {
                reason_string,
                user_properties,
            } = other_properties;

            encode_properties! {
                dst,
                reason_string: Option<ReasonString>,
                user_properties: Vec<UserProperty>,
            }
        }

        for &reason_code in reason_codes {
            dst.try_put_u8(reason_code.to_u8(version))
                .ok_or(EncodeError::InsufficientBuffer)?;
        }

        Ok(())
    }
}

impl<S> SubAckOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `SubAckOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<SubAckOtherProperties<O2::Shared>, buffer_pool::Error>
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

        Ok(SubAckOtherProperties {
            reason_string,
            user_properties,
        })
    }
}

mod v3_suback_reason_codes {
    pub(super) const SUCCESS_QOS0: u8 = 0x00;
    pub(super) const SUCCESS_QOS1: u8 = 0x01;
    pub(super) const SUCCESS_QOS2: u8 = 0x02;
    pub(super) const FAILURE: u8 = 0x80;
}

impl SubscribeReasonCode {
    fn try_from(code: u8, version: ProtocolVersion) -> Result<Self, DecodeError> {
        Ok(match (version, code) {
            (ProtocolVersion::V3, v3_suback_reason_codes::SUCCESS_QOS0) => Self::GrantedQoS0,
            (ProtocolVersion::V3, v3_suback_reason_codes::SUCCESS_QOS1) => Self::GrantedQoS1,
            (ProtocolVersion::V3, v3_suback_reason_codes::SUCCESS_QOS2) => Self::GrantedQoS2,
            (ProtocolVersion::V3, v3_suback_reason_codes::FAILURE) => Self::UnspecifiedError,
            (ProtocolVersion::V3, code) => return Err(DecodeError::UnrecognizedQoS(code)),
            (ProtocolVersion::V5, code) => code.try_into()?,
        })
    }

    fn to_u8(self, version: ProtocolVersion) -> u8 {
        match (version, self) {
            (ProtocolVersion::V3, Self::GrantedQoS0) => v3_suback_reason_codes::SUCCESS_QOS0,
            (ProtocolVersion::V3, Self::GrantedQoS1) => v3_suback_reason_codes::SUCCESS_QOS1,
            (ProtocolVersion::V3, Self::GrantedQoS2) => v3_suback_reason_codes::SUCCESS_QOS2,
            (ProtocolVersion::V3, _) => v3_suback_reason_codes::FAILURE,
            (ProtocolVersion::V5, code) => code.into(),
        }
    }
}

impl From<QoS> for SubscribeReasonCode {
    fn from(qos: QoS) -> Self {
        match qos {
            QoS::AtMostOnce => Self::GrantedQoS0,
            QoS::AtLeastOnce => Self::GrantedQoS1,
            QoS::ExactlyOnce => Self::GrantedQoS2,
        }
    }
}

#[cfg(test)]
mod tests {
    use test_case::test_case;

    use super::*;
    use crate::azure_mqtt::mqtt_proto::Packet;

    #[test_case(
        ProtocolVersion::V3,
        SubscribeReasonCode::GrantedQoS0,
        v3_suback_reason_codes::SUCCESS_QOS0
    )]
    #[test_case(
        ProtocolVersion::V3,
        SubscribeReasonCode::GrantedQoS1,
        v3_suback_reason_codes::SUCCESS_QOS1
    )]
    #[test_case(
        ProtocolVersion::V3,
        SubscribeReasonCode::GrantedQoS2,
        v3_suback_reason_codes::SUCCESS_QOS2
    )]
    #[test_case(
        ProtocolVersion::V3,
        SubscribeReasonCode::UnspecifiedError,
        v3_suback_reason_codes::FAILURE
    )]
    #[test_case(
        ProtocolVersion::V5,
        SubscribeReasonCode::ImplementationSpecificError,
        0x83
    )]
    fn reason_code_to_u8(proto: ProtocolVersion, code: SubscribeReasonCode, expect: u8) {
        assert_eq!(code.to_u8(proto), expect);
    }

    #[test_case(QoS::AtMostOnce, SubscribeReasonCode::GrantedQoS0; "qos 0")]
    #[test_case(QoS::AtLeastOnce, SubscribeReasonCode::GrantedQoS1; "qos 1")]
    #[test_case(QoS::ExactlyOnce, SubscribeReasonCode::GrantedQoS2; "qos 2")]
    fn subscribe_reason_code_from_qos(qos: QoS, code: SubscribeReasonCode) {
        assert_eq!(SubscribeReasonCode::from(qos), code);
    }

    encode_decode_v3! {
        Packet::SubAck(SubAck {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            reason_codes: vec![
                SubscribeReasonCode::GrantedQoS1,
            ],
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::SubAck(SubAck {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            reason_codes: vec![
                SubscribeReasonCode::GrantedQoS0,
                SubscribeReasonCode::GrantedQoS1,
                SubscribeReasonCode::GrantedQoS2,
                SubscribeReasonCode::UnspecifiedError,
                SubscribeReasonCode::ImplementationSpecificError,
                SubscribeReasonCode::NotAuthorized,
                SubscribeReasonCode::TopicFilterInvalid,
                SubscribeReasonCode::PacketIdentifierInUse,
                SubscribeReasonCode::QuotaExceeded,
                SubscribeReasonCode::SharedSubscriptionsNotSupported,
                SubscribeReasonCode::SubscriptionIdentifiersNotSupported,
                SubscribeReasonCode::WildcardSubscriptionsNotSupported,
            ],
            other_properties: SubAckOtherProperties {
                reason_string: Some("reason".into()),
                user_properties: vec![("foo".into(), "bar".into())],
            },
        }),
    }
}
