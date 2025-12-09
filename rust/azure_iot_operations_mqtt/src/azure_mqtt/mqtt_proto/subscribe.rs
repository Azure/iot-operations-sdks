// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::num::NonZeroU32;

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, Filter, PacketIdentifier, ProtocolVersion, QoS,
    SharedExt as _, UserProperties,
};
use crate::azure_mqtt::mqtt_proto::{PacketMeta, Property, PropertyRef};

/// Ref: 3.8 SUBSCRIBE - Subscribe to topics
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
#[allow(clippy::struct_field_names)]
pub struct Subscribe<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub subscribe_to: Vec<SubscribeTo<S>>,
    pub other_properties: SubscribeOtherProperties<S>,
}

/// A subscription request.
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct SubscribeTo<S>
where
    S: Shared,
{
    pub topic_filter: Filter<ByteStr<S>>,
    pub options: SubscribeOptions,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
pub struct SubscribeOptions {
    pub maximum_qos: QoS,
    pub other_properties: SubscribeOptionsOtherProperties,
}

impl SubscribeOptions {
    pub fn decode<S>(src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError>
    where
        S: Shared,
    {
        match version {
            ProtocolVersion::V3 => {
                let maximum_qos = match src.try_get_u8()? {
                    0x00 => QoS::AtMostOnce,
                    0x01 => QoS::AtLeastOnce,
                    0x02 => QoS::ExactlyOnce,
                    maximum_qos => return Err(DecodeError::UnrecognizedQoS(maximum_qos)),
                };

                Ok(Self {
                    maximum_qos,
                    other_properties: Default::default(),
                })
            }

            ProtocolVersion::V5 => {
                let options = src.try_get_u8()?;
                let maximum_qos = (options & 0b0000_0011).try_into()?;
                let no_local = (options & 0b0000_0100) != 0;
                let retain_as_published = (options & 0b0000_1000) != 0;
                let retain_handling = ((options & 0b0011_0000) >> 4).try_into()?;

                if (options & 0b1100_0000) != 0 {
                    return Err(DecodeError::SubscriptionOptionsReservedSet);
                }

                Ok(Self {
                    maximum_qos,
                    other_properties: SubscribeOptionsOtherProperties {
                        no_local,
                        retain_as_published,
                        retain_handling,
                    },
                })
            }
        }
    }

    #[allow(clippy::trivially_copy_pass_by_ref)]
    pub fn encode<B, S>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
        S: Shared,
    {
        match version {
            ProtocolVersion::V3 => {
                dst.try_put_u8(self.maximum_qos.into())
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }

            ProtocolVersion::V5 => {
                let mut subscription_options = 0_u8;
                subscription_options |= u8::from(self.maximum_qos);
                if self.other_properties.no_local {
                    subscription_options |= 0b0000_0100;
                }
                if self.other_properties.retain_as_published {
                    subscription_options |= 0b0000_1000;
                }
                subscription_options |= u8::from(self.other_properties.retain_handling) << 4;

                dst.try_put_u8(subscription_options)
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }
        }

        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
pub struct SubscribeOptionsOtherProperties {
    pub no_local: bool,
    pub retain_as_published: bool,
    pub retain_handling: RetainHandling,
}

define_u8_code! {
    /// Ref: 3.8.3.1 Subscription Options
    enum RetainHandling,
    UnrecognizedRetainHandling,
    Send = 0x00,
    SendOnlyIfSubscriptionDoesNotCurrentlyExist = 0x01,
    DoNotSend = 0x02,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct SubscribeOtherProperties<S>
where
    S: Shared,
{
    pub subscription_identifier: Option<NonZeroU32>,
    pub user_properties: UserProperties<S>,
}

impl<S> Subscribe<S>
where
    S: Shared,
{
    /// Creates a copy of this `Subscribe` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<Subscribe<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let mut new_subscribe_to = Vec::with_capacity(self.subscribe_to.len());
        for subscribe_to in &self.subscribe_to {
            new_subscribe_to.push(subscribe_to.to_shared(owned)?);
        }

        Ok(Subscribe {
            packet_identifier: self.packet_identifier,
            subscribe_to: new_subscribe_to,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> PacketMeta<S> for Subscribe<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x80;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        let other_properties = SubscribeOtherProperties::decode(src, version)?;

        let mut subscribe_to = vec![];

        while !src.is_empty() {
            subscribe_to.push(SubscribeTo::decode(src, version)?);
        }

        if subscribe_to.is_empty() {
            return Err(DecodeError::NoTopics);
        }

        Ok(Self {
            packet_identifier,
            subscribe_to,
            other_properties,
        })
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            packet_identifier,
            subscribe_to,
            other_properties,
        } = self;

        dst.try_put_u16_be(packet_identifier.0.get())
            .ok_or(EncodeError::InsufficientBuffer)?;

        other_properties.encode(dst, version)?;

        if subscribe_to.is_empty() {
            return Err(EncodeError::NoTopics);
        }

        for subscribe_to in subscribe_to {
            subscribe_to.encode(dst, version)?;
        }

        Ok(())
    }
}

impl<S> SubscribeTo<S>
where
    S: Shared,
{
    pub fn decode(src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let topic_filter = Filter::decode(src)?.ok_or(DecodeError::IncompletePacket)?;

        let options = SubscribeOptions::decode(src, version)?;

        if topic_filter.is_shared() && options.other_properties.no_local {
            // [MQTT-3.8.3-4].
            return Err(DecodeError::NoLocalWithSharedSubscription);
        }

        Ok(Self {
            topic_filter,
            options,
        })
    }

    pub fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        self.topic_filter.encode(dst)?;

        self.options.encode(dst, version)?;

        Ok(())
    }

    /// Creates a copy of this `SubscribeTo` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<SubscribeTo<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        Ok(SubscribeTo {
            topic_filter: self.topic_filter.to_shared(owned)?,
            options: self.options,
        })
    }
}

impl Default for SubscribeOptionsOtherProperties {
    fn default() -> Self {
        Self {
            no_local: false,
            retain_as_published: true,
            retain_handling: RetainHandling::Send,
        }
    }
}

impl<S> SubscribeOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `SubscribeOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<SubscribeOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        Ok(SubscribeOtherProperties {
            subscription_identifier: self.subscription_identifier,
            user_properties,
        })
    }
}

impl<S> SubscribeOtherProperties<S>
where
    S: Shared,
{
    pub fn decode(src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        Ok(match version {
            ProtocolVersion::V3 => Default::default(),

            ProtocolVersion::V5 => {
                decode_properties!(
                    src,
                    subscription_identifier: SubscriptionIdentifier,
                    user_properties: Vec<UserProperty>,
                );

                SubscribeOtherProperties {
                    subscription_identifier,
                    user_properties,
                }
            }
        })
    }

    pub fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        if version.is_v5() {
            let SubscribeOtherProperties {
                subscription_identifier,
                user_properties,
            } = self;

            encode_properties! {
                dst,
                subscription_identifier: Option<SubscriptionIdentifier>,
                user_properties: Vec<UserProperty>,
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use std::num::NonZeroU32;

    use bytes::Bytes;
    use matches::assert_matches;
    use test_case::test_case;

    use super::{
        RetainHandling, Subscribe, SubscribeOptions, SubscribeOptionsOtherProperties,
        SubscribeOtherProperties, SubscribeTo,
    };
    use crate::azure_mqtt::buffer_pool::Shared;
    use crate::azure_mqtt::mqtt_proto::{
        BinaryData, DecodeError, Packet, PacketIdentifier, ProtocolVersion, QoS, filter,
    };

    encode_decode_v3! {
        Packet::Subscribe(Subscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![
                SubscribeTo {
                    topic_filter: filter("foo/bar"),
                    options: SubscribeOptions {
                        maximum_qos: QoS::AtLeastOnce,
                        other_properties: Default::default(),
                    },
                }
            ],
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::Subscribe(Subscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![
                SubscribeTo {
                    topic_filter: filter("foo/bar"),
                    options: SubscribeOptions {
                        maximum_qos: QoS::AtLeastOnce,
                        other_properties: SubscribeOptionsOtherProperties {
                            no_local: true,
                            retain_as_published: true,
                            retain_handling: RetainHandling::DoNotSend
                        },
                    },
                }
            ],
            other_properties: SubscribeOtherProperties {
                subscription_identifier: Some(NonZeroU32::new(1).unwrap()),
                user_properties: vec![("cat".into(), "dog".into())],
            },
        }),
    }

    #[test_case(b"\x82!\0\x02\r\x0b\x01&\0\x03cat\0\x03dog\0\x0e$share/foo/bar-")]
    fn no_local_for_shared_subscription(encoding: &[u8]) {
        let encoding = BinaryData::<Bytes>::from(encoding);
        let mut buffer = encoding.into_shared();
        buffer.drain(std::mem::size_of::<u16>());

        assert_matches!(
            Packet::decode_full(&mut buffer, ProtocolVersion::V5,),
            Err(DecodeError::NoLocalWithSharedSubscription)
        );
    }
}
