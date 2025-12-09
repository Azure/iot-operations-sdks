// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};
use crate::azure_mqtt::mqtt_proto::{
    ByteStr, DecodeError, EncodeError, Filter, PacketIdentifier, PacketMeta, Property, PropertyRef,
    ProtocolVersion, SharedExt as _, UserProperties,
};

/// Ref: 3.10 UNSUBSCRIBE – Unsubscribe from topics
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
#[allow(clippy::struct_field_names)]
pub struct Unsubscribe<S>
where
    S: Shared,
{
    pub packet_identifier: PacketIdentifier,
    pub unsubscribe_from: Vec<Filter<ByteStr<S>>>,
    pub other_properties: UnsubscribeOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Default, Eq, PartialEq)]
pub struct UnsubscribeOtherProperties<S>
where
    S: Shared,
{
    pub user_properties: UserProperties<S>,
}

impl<S> Unsubscribe<S>
where
    S: Shared,
{
    /// Creates a copy of this `Unsubscribe` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Unsubscribe<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let mut new_unsubscribe_from = Vec::with_capacity(self.unsubscribe_from.len());
        for unsubscribe_from in &self.unsubscribe_from {
            new_unsubscribe_from.push(unsubscribe_from.to_shared(owned)?);
        }

        Ok(Unsubscribe {
            packet_identifier: self.packet_identifier,
            unsubscribe_from: new_unsubscribe_from,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> PacketMeta<S> for Unsubscribe<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0xA0;

    fn decode(_flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let packet_identifier = src.try_get_packet_identifier()?;

        let other_properties = match version {
            ProtocolVersion::V3 => Default::default(),

            ProtocolVersion::V5 => {
                decode_properties!(src, user_properties: Vec<UserProperty>,);

                UnsubscribeOtherProperties { user_properties }
            }
        };

        let mut unsubscribe_from = vec![];

        while !src.is_empty() {
            unsubscribe_from.push(Filter::decode(src)?.ok_or(DecodeError::IncompletePacket)?);
        }

        if unsubscribe_from.is_empty() {
            return Err(DecodeError::NoTopics);
        }

        Ok(Self {
            packet_identifier,
            unsubscribe_from,
            other_properties,
        })
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            packet_identifier,
            unsubscribe_from,
            other_properties,
        } = self;

        dst.try_put_u16_be(packet_identifier.0.get())
            .ok_or(EncodeError::InsufficientBuffer)?;

        if version.is_v5() {
            let UnsubscribeOtherProperties { user_properties } = other_properties;

            encode_properties! {
                dst,
                user_properties: Vec<UserProperty>,
            }
        }

        if unsubscribe_from.is_empty() {
            return Err(EncodeError::NoTopics);
        }

        for unsubscribe_from in unsubscribe_from {
            unsubscribe_from.encode(dst)?;
        }

        Ok(())
    }
}

impl<S> UnsubscribeOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `UnsubscribeOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<UnsubscribeOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        Ok(UnsubscribeOtherProperties { user_properties })
    }
}

#[cfg(test)]
mod tests {
    use super::{Unsubscribe, UnsubscribeOtherProperties};

    use crate::azure_mqtt::mqtt_proto::{Packet, PacketIdentifier, filter};

    encode_decode_v3! {
        Packet::Unsubscribe(Unsubscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            unsubscribe_from: vec![
                filter("foo/bar"),
            ],
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::Unsubscribe(Unsubscribe {
            packet_identifier: PacketIdentifier::new(0x1234).unwrap(),
            unsubscribe_from: vec![filter("topic")],
            other_properties: UnsubscribeOtherProperties {
                user_properties: vec![("foo".into(), "bar".into())],
            },
        }),
    }
}
