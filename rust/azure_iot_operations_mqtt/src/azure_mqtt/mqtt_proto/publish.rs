// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::num::{NonZeroU16, NonZeroU32};

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    BinaryData, ByteStr, DecodeError, EncodeError, PacketIdentifierDupQoS, PacketMeta, Property,
    PropertyRef, ProtocolVersion, PublicationOtherProperties, SharedExt as _, Topic,
    UserProperties,
};

/// 3.3 PUBLISH – Publish message
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct Publish<S>
where
    S: Shared,
{
    pub topic_name: Topic<ByteStr<S>>,
    pub packet_identifier_dup_qos: PacketIdentifierDupQoS,
    pub retain: bool,
    pub payload: S,
    pub other_properties: PublishOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct PublishOtherProperties<S>
where
    S: Shared,
{
    pub payload_is_utf8: bool,
    pub message_expiry_interval: Option<u32>,
    pub topic_alias: Option<NonZeroU16>,
    pub response_topic: Option<Topic<ByteStr<S>>>,
    pub correlation_data: Option<BinaryData<S>>,
    pub user_properties: UserProperties<S>,
    pub subscription_identifiers: Vec<NonZeroU32>,
    pub content_type: Option<ByteStr<S>>,
}

impl<S> Publish<S>
where
    S: Shared,
{
    /// Creates a copy of this `Publish` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<Publish<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let payload = self.payload.copy_to_shared(owned)?;

        Ok(Publish {
            topic_name: self.topic_name.to_shared(owned)?,
            packet_identifier_dup_qos: self.packet_identifier_dup_qos,
            retain: self.retain,
            payload,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> From<PublicationOtherProperties<S>> for PublishOtherProperties<S>
where
    S: Shared,
{
    fn from(props: PublicationOtherProperties<S>) -> Self {
        Self {
            payload_is_utf8: props.payload_is_utf8,
            message_expiry_interval: props.message_expiry_interval,
            response_topic: props.response_topic,
            correlation_data: props.correlation_data,
            user_properties: props.user_properties,
            content_type: props.content_type,
            ..Default::default()
        }
    }
}

impl<S> PacketMeta<S> for Publish<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x30;

    fn decode(flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let dup = (flags & 0b0000_1000) != 0;
        let retain = (flags & 0b0000_0001) != 0;

        let topic_name = Topic::decode(src)?.ok_or(DecodeError::IncompletePacket)?;

        let packet_identifier_dup_qos = match (flags & 0b0000_0110) >> 1 {
            0x00 if dup => return Err(DecodeError::PublishDupAtMostOnce),

            0x00 => PacketIdentifierDupQoS::AtMostOnce,

            0x01 => {
                let packet_identifier = src.try_get_packet_identifier()?;
                PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, dup)
            }

            0x02 => {
                let packet_identifier = src.try_get_packet_identifier()?;
                PacketIdentifierDupQoS::ExactlyOnce(packet_identifier, dup)
            }

            qos => return Err(DecodeError::UnrecognizedQoS(qos)),
        };

        match version {
            ProtocolVersion::V3 => {
                let payload = src.split_to(src.len());

                Ok(Self {
                    topic_name,
                    packet_identifier_dup_qos,
                    retain,
                    payload,
                    other_properties: Default::default(),
                })
            }

            ProtocolVersion::V5 => {
                decode_properties!(
                    src,
                    payload_is_utf8: PayloadIsUtf8,
                    message_expiry_interval: MessageExpiryInterval,
                    topic_alias: TopicAlias,
                    response_topic: ResponseTopic,
                    correlation_data: CorrelationData,
                    user_properties: Vec<UserProperty>,
                    subscription_identifiers: Vec<SubscriptionIdentifier>,
                    content_type: ContentType,
                );

                let payload = src.split_to(src.len());

                Ok(Self {
                    topic_name,
                    packet_identifier_dup_qos,
                    retain,
                    payload,
                    other_properties: PublishOtherProperties {
                        payload_is_utf8: payload_is_utf8.unwrap_or_default(),
                        message_expiry_interval,
                        topic_alias,
                        response_topic,
                        correlation_data,
                        user_properties,
                        subscription_identifiers,
                        content_type,
                    },
                })
            }
        }
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            topic_name,
            packet_identifier_dup_qos,
            retain: _,
            payload,
            other_properties,
        } = self;

        topic_name.encode(dst)?;

        match packet_identifier_dup_qos {
            PacketIdentifierDupQoS::AtMostOnce => (),
            PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, _)
            | PacketIdentifierDupQoS::ExactlyOnce(packet_identifier, _) => {
                dst.try_put_u16_be(packet_identifier.0.get())
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }
        }

        if version.is_v5() {
            let PublishOtherProperties {
                payload_is_utf8,
                message_expiry_interval,
                topic_alias,
                response_topic,
                correlation_data,
                user_properties,
                subscription_identifiers,
                content_type,
            } = other_properties;

            encode_properties! {
                dst,
                payload_is_utf8: PayloadIsUtf8,
                message_expiry_interval: Option<MessageExpiryInterval>,
                topic_alias: Option<TopicAlias>,
                response_topic: Option<ResponseTopic>,
                correlation_data: Option<CorrelationData>,
                user_properties: Vec<UserProperty>,
                subscription_identifiers: Vec<SubscriptionIdentifier>,
                content_type: Option<ContentType>,
            }
        }

        dst.put_shared(payload.clone());

        Ok(())
    }
}

impl<S> PublishOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `PublishOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<PublishOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let response_topic = match &self.response_topic {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let correlation_data = match &self.correlation_data {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        let content_type = match &self.content_type {
            Some(value) => Some(value.to_shared(owned)?),
            None => None,
        };

        Ok(PublishOtherProperties {
            payload_is_utf8: self.payload_is_utf8,
            message_expiry_interval: self.message_expiry_interval,
            topic_alias: self.topic_alias,
            response_topic,
            correlation_data,
            user_properties,
            subscription_identifiers: self.subscription_identifiers.clone(),
            content_type,
        })
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;

    use super::*;
    use crate::azure_mqtt::buffer_pool::{BufferPool as _, BytesPool};
    use crate::azure_mqtt::mqtt_proto::{Packet, PacketIdentifier, topic};

    encode_decode_v3! {
        Packet::Publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(PacketIdentifier::new(1).unwrap(), false),
            retain: false,
            topic_name: topic("foo/bar"),
            payload: Bytes::from_static(b"hello world"),
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::Publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: topic("foo/bar"),
            payload: Bytes::from_static(b"hello world"),
            other_properties: PublishOtherProperties {
                user_properties: vec![("hello".into(), "world".into())],
                ..Default::default()
            },
        }),

        Packet::Publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(PacketIdentifier::new(1).unwrap(), false),
            retain: false,
            topic_name: topic("foo/bar"),
            payload: Bytes::from_static(b"hello world"),
            other_properties: PublishOtherProperties {
                user_properties: vec![("hello".into(), "world".into())],
                ..Default::default()
            },
        }),

        Packet::Publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::ExactlyOnce(PacketIdentifier::new(1).unwrap(), false),
            retain: false,
            topic_name: topic("foo/bar"),
            payload: Bytes::from_static(b"hello world"),
            other_properties: PublishOtherProperties {
                user_properties: vec![("hello".into(), "world".into())],
                ..Default::default()
            },
        }),

        Packet::Publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(PacketIdentifier::new(1).unwrap(), false),
            retain: false,
            topic_name: topic("foo/bar"),
            payload: Bytes::from_static(b"hello world"),
            other_properties: PublishOtherProperties {
                user_properties: vec![("hello".into(), "world".into())],
                payload_is_utf8: true,
                message_expiry_interval: Some(10),
                topic_alias: Some(NonZeroU16::new(16).unwrap()),
                response_topic: Some(topic("response/topic")),
                correlation_data: Some(b"cd".into()),
                subscription_identifiers: vec![NonZeroU32::new(1).unwrap(), NonZeroU32::new(2).unwrap()],
                content_type: Some("stuff".into()),
            },
        }),
    }

    #[test]
    fn test_to_shared() {
        let publish = Publish {
            topic_name: topic("kittens"),
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            payload: Bytes::from_static(b"meow"),
            other_properties: PublishOtherProperties {
                response_topic: Some(topic("cute")),
                user_properties: vec![("genus".into(), "felix".into())],
                correlation_data: Some(b"corr_data".into()),
                ..Default::default()
            },
        };

        let pool = BytesPool;
        let mut owned = pool.take_empty_owned();

        let publish_shared = publish.to_shared(&mut owned).unwrap();

        assert_eq!(publish, publish_shared);
    }
}
