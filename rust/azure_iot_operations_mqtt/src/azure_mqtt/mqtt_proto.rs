// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// TODO: Revisit these suppressions
#![allow(dead_code)]
#![allow(unused_imports)]

/*!
 * MQTT protocol types for versions 3.1.1 and 5.0.
 *
 * Ref:
 * - <https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html>
 * - <https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html>
 */

use std::{cmp::Ordering, hash::Hash, num::NonZeroU16};

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};

const PROTOCOL_NAME_STR: &str = "MQTT";
const PROTOCOL_NAME: &[u8] = b"\x00\x04MQTT";

const PROTOCOL_VERSION_V3: u8 = 0x04;

const PROTOCOL_VERSION_V5: u8 = 0x05;

/// The size of the smallest MQTT packet. This is the PINGREQ and PINGRESP packets
/// that are both 2 bytes.
pub const SMALLEST_PACKET_SIZE: usize = 2;

pub const DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH: usize = 4;

macro_rules! define_u8_code {
    (
        $(#[$meta:meta])*
        enum $ty:ident,
        $error_variant:ident,
        $($variant:ident = $value:expr ,)*
    ) => {
        $(#[$meta])*
        #[derive(Clone, Copy, Debug, Eq, Hash, PartialEq, Ord, PartialOrd, enum_iterator::Sequence)]
        #[allow(clippy::enum_variant_names)]
        pub enum $ty {
            $($variant),*
        }

        impl TryFrom<u8> for $ty {
            type Error = DecodeError;

            fn try_from(code: u8) -> Result<Self, Self::Error> {
                Ok(match code {
                    $($value => $ty::$variant ,)*
                    code => return Err(DecodeError::$error_variant(code)),
                })
            }
        }

        impl From<$ty> for u8 {
            fn from(code: $ty) -> Self {
                match code {
                    $($ty::$variant => $value ,)*
                }
            }
        }
    };
}

#[cfg(test)]
macro_rules! encode_decode_v3 {
    (
        @test_cases
        { $($attrs:tt)* }
        { $packet:expr, $($rest:tt)* }
    ) => {
        encode_decode_v3! {
            @test_cases
            { $($attrs)* #[test_case::test_case($packet, None)] }
            { $($rest)* }
        }
    };

    (
        @test_cases
        { $($attrs:tt)* }
        { $packet:expr => $encoded:expr, $($rest:tt)* }
    ) => {
        encode_decode_v3! {
            @test_cases
            { $($attrs)* #[test_case::test_case($packet, Some(&$encoded[..]))] }
            { $($rest)* }
        }
    };

    (
        @test_cases
        { $($attrs:tt)* }
        { }
    ) => {
        $($attrs)*
        fn encode_decode_v3(packet: Packet<bytes::Bytes>, expected_encoding: Option<&[u8]>) {
            let mut encoding = crate::azure_mqtt::mqtt_proto::tests::encode(&packet, crate::azure_mqtt::mqtt_proto::ProtocolVersion::V3);
            println!("encoded packet: {encoding:?}");
            if let Some(expected_encoding) = expected_encoding {
                assert_eq!(expected_encoding, encoding.as_ref());
            }
            let decoded_packet = crate::azure_mqtt::mqtt_proto::tests::decode(&mut encoding, crate::azure_mqtt::mqtt_proto::ProtocolVersion::V3);

            assert_eq!(packet, decoded_packet);
        }
    };

    ($($input:tt)*) => {
        encode_decode_v3! {
            @test_cases
            { }
            { $($input)* }
        }
    };
}

#[cfg(test)]
macro_rules! encode_decode_v5 {
    (
        @test_cases
        { $($attrs:tt)* }
        { $packet:expr, $($rest:tt)* }
    ) => {
        encode_decode_v5! {
            @test_cases
            { $($attrs)* #[test_case::test_case($packet, None)] }
            { $($rest)* }
        }
    };

    (
        @test_cases
        { $($attrs:tt)* }
        { $packet:expr => $encoded:expr, $($rest:tt)* }
    ) => {
        encode_decode_v5! {
            @test_cases
            { $($attrs)* #[test_case::test_case($packet, Some(&$encoded[..]))] }
            { $($rest)* }
        }
    };

    (
        @test_cases
        { $($attrs:tt)* }
        { }
    ) => {
        $($attrs)*
        fn encode_decode_v5(packet: Packet<bytes::Bytes>, expected_encoding: Option<&[u8]>) {
            let mut encoding = crate::azure_mqtt::mqtt_proto::tests::encode(&packet, crate::azure_mqtt::mqtt_proto::ProtocolVersion::V5);
            println!("encoded packet: {encoding:?}");
            if let Some(expected_encoding) = expected_encoding {
                assert_eq!(expected_encoding, encoding.as_ref());
            }
            let decoded_packet = crate::azure_mqtt::mqtt_proto::tests::decode(&mut encoding, crate::azure_mqtt::mqtt_proto::ProtocolVersion::V5);

            assert_eq!(packet, decoded_packet);
        }
    };

    ($($input:tt)*) => {
        encode_decode_v5! {
            @test_cases
            { }
            { $($input)* }
        }
    };
}

mod binary_data;
pub use binary_data::BinaryData;

mod buffer;
pub use buffer::SharedExt;

mod byte_counter;
pub use byte_counter::ByteCounter;

mod byte_str;
pub use byte_str::ByteStr;

mod filter;
#[cfg(any(test, feature = "test-utils"))]
pub use filter::filter;
pub use filter::{ClassifiedFilter, Filter};
#[cfg(test)]
pub use filter::{filter_owned, filter_str};

mod topic;
pub use topic::Topic;
#[cfg(any(test, feature = "test-utils"))]
pub use topic::topic;
#[cfg(test)]
pub use topic::topic_str;

#[macro_use]
mod property;
pub use property::{KeepAlive, Property, PropertyRef, SessionExpiryInterval};

mod auth;
pub use auth::{Auth, AuthenticateReasonCode};

mod connack;
pub use connack::{ConnAck, ConnAckOtherProperties, ConnectReasonCode, ConnectionRefusedReason};

mod connect;
pub use connect::{Connect, ConnectOtherProperties};

mod disconnect;
pub use disconnect::{Disconnect, DisconnectOtherProperties, DisconnectReasonCode};

mod pingreq;
pub use pingreq::PingReq;

mod pingresp;
pub use pingresp::PingResp;

mod puback;
pub use puback::{PubAck, PubAckOtherProperties, PubAckReasonCode};

mod pubcomp;
pub use pubcomp::{PubComp, PubCompOtherProperties, PubCompReasonCode};

mod pubrec;
pub use pubrec::{PubRec, PubRecOtherProperties, PubRecReasonCode};

mod pubrel;
pub use pubrel::{PubRel, PubRelOtherProperties, PubRelReasonCode};

mod publish;
pub use publish::{Publish, PublishOtherProperties};

mod publication;
pub use publication::{Publication, PublicationOtherProperties};

mod suback;
pub use suback::{SubAck, SubAckOtherProperties, SubscribeReasonCode};

mod subscribe;
pub use subscribe::{
    RetainHandling, Subscribe, SubscribeOptions, SubscribeOptionsOtherProperties,
    SubscribeOtherProperties, SubscribeTo,
};

mod unsuback;
pub use unsuback::{UnsubAck, UnsubAckOtherProperties, UnsubAckReasonCode};

mod unsubscribe;
pub use unsubscribe::{Unsubscribe, UnsubscribeOtherProperties};

/// An MQTT packet.
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub enum Packet<S>
where
    S: Shared,
{
    /// Ref: 3.15 AUTH – Authentication exchange
    Auth(Auth<S>),

    /// Ref: 3.2 CONNACK – Connect acknowledgement
    ConnAck(ConnAck<S>),

    /// Ref: 3.1 CONNECT – Connection Request
    Connect(Connect<S>),

    /// Ref: 3.14 DISCONNECT - Disconnect notification
    Disconnect(Disconnect<S>),

    /// Ref: 3.12 PINGREQ – PING request
    PingReq(PingReq),

    /// Ref: 3.13 PINGRESP – PING response
    PingResp(PingResp),

    /// Ref: 3.4 PUBACK – Publish acknowledgement
    PubAck(PubAck<S>),

    /// Ref: 3.7 PUBCOMP – Publish complete (QoS 2 delivery part 3)
    PubComp(PubComp<S>),

    /// 3.3 PUBLISH – Publish message
    Publish(Publish<S>),

    /// Ref: 3.5 PUBREC – Publish received (QoS 2 delivery part 1)
    PubRec(PubRec<S>),

    /// Ref: 3.6 PUBREL – Publish release (QoS 2 delivery part 2)
    PubRel(PubRel<S>),

    /// Ref: 3.9 SUBACK – Subscribe acknowledgement
    SubAck(SubAck<S>),

    /// Ref: 3.8 SUBSCRIBE - Subscribe request
    Subscribe(Subscribe<S>),

    /// Ref: 3.11 UNSUBACK – Unsubscribe acknowledgement
    UnsubAck(UnsubAck<S>),

    /// Ref: 3.10 UNSUBSCRIBE – Unsubscribe request
    Unsubscribe(Unsubscribe<S>),
}

impl<S> Packet<S>
where
    S: Shared,
{
    /// Decode the body (variable header + payload) of an MQTT packet.
    ///
    /// Ref: 2 MQTT Control Packet format
    pub fn decode(
        first_byte: u8,
        body: &mut S,
        version: ProtocolVersion,
    ) -> Result<Self, DecodeError> {
        let packet_type = first_byte & 0xF0;
        let flags = first_byte & 0x0F;

        let packet = match (packet_type, flags) {
            (Auth::<S>::PACKET_TYPE, 0) => Packet::Auth(Auth::decode(flags, body, version)?),

            (ConnAck::<S>::PACKET_TYPE, 0) => {
                Packet::ConnAck(ConnAck::decode(flags, body, version)?)
            }

            (Connect::<S>::PACKET_TYPE, 0) => {
                Packet::Connect(Connect::decode(flags, body, version)?)
            }

            (Disconnect::<S>::PACKET_TYPE, 0) => {
                Packet::Disconnect(Disconnect::decode(flags, body, version)?)
            }

            (<PingReq as PacketMeta<S>>::PACKET_TYPE, 0) => {
                Packet::PingReq(PingReq::decode(flags, body, version)?)
            }

            (<PingResp as PacketMeta<S>>::PACKET_TYPE, 0) => {
                Packet::PingResp(PingResp::decode(flags, body, version)?)
            }

            (PubAck::<S>::PACKET_TYPE, 0) => Packet::PubAck(PubAck::decode(flags, body, version)?),

            (PubComp::<S>::PACKET_TYPE, 0) => {
                Packet::PubComp(PubComp::decode(flags, body, version)?)
            }

            (Publish::<S>::PACKET_TYPE, flags) => {
                Packet::Publish(Publish::decode(flags, body, version)?)
            }

            (PubRec::<S>::PACKET_TYPE, 0) => Packet::PubRec(PubRec::decode(flags, body, version)?),

            (PubRel::<S>::PACKET_TYPE, 2) => Packet::PubRel(PubRel::decode(flags, body, version)?),

            (SubAck::<S>::PACKET_TYPE, 0) => Packet::SubAck(SubAck::decode(flags, body, version)?),

            (Subscribe::<S>::PACKET_TYPE, 2) => {
                Packet::Subscribe(Subscribe::decode(flags, body, version)?)
            }

            (UnsubAck::<S>::PACKET_TYPE, 0) => {
                Packet::UnsubAck(UnsubAck::decode(flags, body, version)?)
            }

            (Unsubscribe::<S>::PACKET_TYPE, 2) => {
                Packet::Unsubscribe(Unsubscribe::decode(flags, body, version)?)
            }

            (packet_type, flags) => {
                return Err(DecodeError::UnrecognizedPacket {
                    packet_type,
                    flags,
                    remaining_length: body.len(),
                });
            }
        };

        if !body.is_empty() {
            return Err(DecodeError::TrailingGarbage);
        }

        Ok(packet)
    }

    /// Decodes a full `Packet` from the given `Shared`.
    pub fn decode_full(src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let first_byte = {
            let mut data = src.as_ref();
            let original_data_len = data.len();

            let (first_byte, remaining_length) =
                decode_fixed_header(&mut data)?.ok_or(DecodeError::IncompletePacket)?;

            let new_data_len = data.len();
            src.drain(original_data_len - new_data_len);

            match src.len().cmp(&remaining_length) {
                Ordering::Less => return Err(DecodeError::IncompletePacket),
                Ordering::Equal => first_byte,
                Ordering::Greater => return Err(DecodeError::TrailingGarbage),
            }
        };

        Self::decode(first_byte, src, version)
    }

    pub fn encode<BA>(&self, dst: &mut BA, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        BA: BytesAccumulator<Shared = S>,
    {
        self.borrow().encode(dst, version)
    }

    pub fn borrow(&self) -> PacketRef<'_, S> {
        match self {
            Self::Auth(packet) => PacketRef::Auth(packet),
            Self::ConnAck(packet) => PacketRef::ConnAck(packet),
            Self::Connect(packet) => PacketRef::Connect(packet),
            Self::Disconnect(packet) => PacketRef::Disconnect(packet),
            Self::PingReq(packet) => PacketRef::PingReq(packet),
            Self::PingResp(packet) => PacketRef::PingResp(packet),
            Self::PubAck(packet) => PacketRef::PubAck(packet),
            Self::PubComp(packet) => PacketRef::PubComp(packet),
            Self::Publish(packet) => PacketRef::Publish(packet),
            Self::PubRec(packet) => PacketRef::PubRec(packet),
            Self::PubRel(packet) => PacketRef::PubRel(packet),
            Self::SubAck(packet) => PacketRef::SubAck(packet),
            Self::Subscribe(packet) => PacketRef::Subscribe(packet),
            Self::UnsubAck(packet) => PacketRef::UnsubAck(packet),
            Self::Unsubscribe(packet) => PacketRef::Unsubscribe(packet),
        }
    }
}

/// Ref to an MQTT packet.
#[derive(Debug)]
#[derive_where(Clone, Copy, Eq, PartialEq)]
pub enum PacketRef<'a, S>
where
    S: Shared,
{
    /// Ref: 3.15 AUTH – Authentication exchange
    Auth(&'a Auth<S>),

    /// Ref: 3.2 CONNACK – Connect acknowledgement
    ConnAck(&'a ConnAck<S>),

    /// Ref: 3.1 CONNECT – Connection Request
    Connect(&'a Connect<S>),

    /// Ref: 3.14 DISCONNECT - Disconnect notification
    Disconnect(&'a Disconnect<S>),

    /// Ref: 3.12 PINGREQ – PING request
    PingReq(&'a PingReq),

    /// Ref: 3.13 PINGRESP – PING response
    PingResp(&'a PingResp),

    /// Ref: 3.4 PUBACK – Publish acknowledgement
    PubAck(&'a PubAck<S>),

    /// Ref: 3.7 PUBCOMP – Publish complete (QoS 2 delivery part 3)
    PubComp(&'a PubComp<S>),

    /// 3.3 PUBLISH – Publish message
    Publish(&'a Publish<S>),

    /// Ref: 3.5 PUBREC – Publish received (QoS 2 delivery part 1)
    PubRec(&'a PubRec<S>),

    /// Ref: 3.6 PUBREL – Publish release (QoS 2 delivery part 2)
    PubRel(&'a PubRel<S>),

    /// Ref: 3.9 SUBACK – Subscribe acknowledgement
    SubAck(&'a SubAck<S>),

    /// Ref: 3.8 SUBSCRIBE - Subscribe request
    Subscribe(&'a Subscribe<S>),

    /// Ref: 3.11 UNSUBACK – Unsubscribe acknowledgement
    UnsubAck(&'a UnsubAck<S>),

    /// Ref: 3.10 UNSUBSCRIBE – Unsubscribe request
    Unsubscribe(&'a Unsubscribe<S>),
}

impl<S> PacketRef<'_, S>
where
    S: Shared,
{
    pub fn encode<BA>(self, dst: &mut BA, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        BA: BytesAccumulator<Shared = S>,
    {
        match self {
            Self::Auth(packet) => encode_inner(packet, 0, dst, version),
            Self::ConnAck(packet) => encode_inner(packet, 0, dst, version),
            Self::Connect(packet) => encode_inner(packet, 0, dst, version),
            Self::Disconnect(packet) => encode_inner(packet, 0, dst, version),
            Self::PingReq(packet) => encode_inner(packet, 0, dst, version),
            Self::PingResp(packet) => encode_inner(packet, 0, dst, version),
            Self::PubAck(packet) => encode_inner(packet, 0, dst, version),
            Self::PubComp(packet) => encode_inner(packet, 0, dst, version),
            Self::Publish(packet) => {
                #[expect(clippy::unusual_byte_groupings)]
                let mut flags = match packet.packet_identifier_dup_qos {
                    PacketIdentifierDupQoS::AtMostOnce => 0b0_00_0,
                    PacketIdentifierDupQoS::AtLeastOnce(_, true) => 0b1_01_0,
                    PacketIdentifierDupQoS::AtLeastOnce(_, false) => 0b0_01_0,
                    PacketIdentifierDupQoS::ExactlyOnce(_, true) => 0b1_10_0,
                    PacketIdentifierDupQoS::ExactlyOnce(_, false) => 0b0_10_0,
                };
                #[expect(clippy::unusual_byte_groupings)]
                if packet.retain {
                    flags |= 0b0_00_1;
                }
                encode_inner(packet, flags, dst, version)
            }
            Self::PubRec(packet) => encode_inner(packet, 0, dst, version),
            Self::PubRel(packet) => encode_inner(packet, 0x02, dst, version),
            Self::SubAck(packet) => encode_inner(packet, 0, dst, version),
            Self::Subscribe(packet) => encode_inner(packet, 0x02, dst, version),
            Self::UnsubAck(packet) => encode_inner(packet, 0, dst, version),
            Self::Unsubscribe(packet) => encode_inner(packet, 0x02, dst, version),
        }
    }
}

fn encode_inner<B, TPacket>(
    packet: &TPacket,
    flags: u8,
    dst: &mut B,
    version: ProtocolVersion,
) -> Result<(), EncodeError>
where
    B: BytesAccumulator,
    TPacket: PacketMeta<B::Shared>,
{
    let body_len = {
        let mut counter = ByteCounter::<_, true>::new();
        packet.encode(&mut counter, version)?;
        counter.into_count()
    };

    dst.try_put_u8(TPacket::PACKET_TYPE | flags)
        .ok_or(EncodeError::InsufficientBuffer)?;
    encode_remaining_length(body_len, dst)?;
    packet.encode(dst, version)?;

    dst.put_done();

    Ok(())
}

/// Version of the MQTT Protocol that client is requested
/// in the CONNECT packet.
#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
pub enum ProtocolVersion {
    V3,
    V5,
}

impl ProtocolVersion {
    pub fn is_v3(self) -> bool {
        matches!(self, Self::V3)
    }

    pub fn is_v5(self) -> bool {
        matches!(self, Self::V5)
    }

    pub fn to_u8(self) -> u8 {
        match self {
            ProtocolVersion::V3 => PROTOCOL_VERSION_V3,
            ProtocolVersion::V5 => PROTOCOL_VERSION_V5,
        }
    }

    pub fn to_str(self) -> &'static str {
        match self {
            ProtocolVersion::V3 => "V3",
            ProtocolVersion::V5 => "V5",
        }
    }
}

impl std::fmt::Display for ProtocolVersion {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(self.to_str())
    }
}

#[derive(Clone, Eq, PartialEq)]
pub struct Authentication<S>
where
    S: Shared,
{
    pub method: ByteStr<S>,
    pub data: Option<BinaryData<S>>,
}

impl<S> std::fmt::Debug for Authentication<S>
where
    S: Shared,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Authentication")
            .field("method", &self.method)
            .finish_non_exhaustive()
    }
}

impl<S> Authentication<S>
where
    S: Shared,
{
    fn of(
        method: Option<ByteStr<S>>,
        data: Option<BinaryData<S>>,
    ) -> Result<Option<Self>, DecodeError> {
        match (method, data) {
            (Some(method), data) => Ok(Some(Authentication { method, data })),

            // Authentication method and data can be set for CONNECT, CONNACK and AUTH. All three of them
            // require that, if data is set, then method must also be set.
            (None, Some(_)) => Err(DecodeError::MissingRequiredProperty(
                "authentication method",
            )),

            (None, None) => Ok(None),
        }
    }

    /// Creates a copy of this `Authentication` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<Authentication<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let data = if let Some(data) = &self.data {
            Some(data.to_shared(owned)?)
        } else {
            None
        };

        Ok(Authentication {
            method: self.method.to_shared(owned)?,
            data,
        })
    }
}

impl<S> Authentication<S>
where
    S: Shared,
{
    // Ideally this would return `(&Option<ByteStr<S>>, &Option<S>)` since that is all
    // that the property encoder requires, but that is not possible.
    fn into_parts(this: Option<&Self>) -> (Option<ByteStr<S>>, &Option<BinaryData<S>>) {
        match this {
            Some(Authentication { method, data }) => (Some(method.clone()), data),
            None => (None, &None),
        }
    }
}

/// Decode MQTT-format variable byte integers.
///
/// These numbers are encoded with a variable-length scheme that uses the MSB of each byte as a continuation bit.
///
/// Ref:
/// - 3.1.1: 2.2.3 Remaining Length
/// - 5.0:   1.5.5 Variable Byte Integer
fn decode_varint(src: &mut &[u8]) -> Result<Option<u32>, DecodeError> {
    let mut result = 0_u32;
    let mut num_bytes_read = 0_usize;

    loop {
        let Some((encoded_byte, rest)) = src.split_first() else {
            return Ok(None);
        };
        *src = rest;

        result |= u32::from(encoded_byte & 0x7F) << (num_bytes_read * 7);
        num_bytes_read += 1;

        if encoded_byte & 0x80 == 0 {
            return Ok(Some(result));
        }

        if num_bytes_read == DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH {
            return Err(DecodeError::VarintTooHigh);
        }
    }
}

/// Encode MQTT-format variable byte integers.
/// See [`decode_varint`] for details.
fn encode_varint<B>(mut item: u32, dst: &mut B) -> Result<(), EncodeError>
where
    B: BytesAccumulator,
{
    let original = item;
    let mut num_bytes_written = 0_usize;

    loop {
        let mut encoded_byte = (item & 0x7F) as u8;

        item >>= 7;

        if item > 0 {
            encoded_byte |= 0x80;
        }

        dst.try_put_u8(encoded_byte)
            .ok_or(EncodeError::InsufficientBuffer)?;
        num_bytes_written += 1;

        if item == 0 {
            break;
        }

        if num_bytes_written == DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH {
            return Err(EncodeError::VarintTooHigh(original));
        }
    }

    Ok(())
}

/// Decode MQTT-format "remaining lengths".
///
/// These are variable byte integers and are implemented in terms of `{decode,encode}_varint`,
/// but work with `usize` instead of `u32`. This makes it convenient to use the remaining length
/// in contexts where Rust code expects a `usize`, like indexing and lengths.
///
/// Ref:
/// - 3.1.1: 2.2.3 Remaining Length
/// - 5.0:   2.1.4 Remaining Length
fn decode_remaining_length(src: &mut &[u8]) -> Result<Option<usize>, DecodeError> {
    const _STATIC_ASSERT_USIZE_IS_ATLEAST_U32: () =
        [(); (std::mem::size_of::<usize>() >= std::mem::size_of::<u32>()) as usize][0];

    decode_varint(src).map(|maybe_varint| {
        maybe_varint.map(|varint| varint.try_into().expect("usize is at least u32"))
    })
}

fn encode_remaining_length<B>(item: usize, dst: &mut B) -> Result<(), EncodeError>
where
    B: BytesAccumulator,
{
    let varint = item
        .try_into()
        .map_err(|_| EncodeError::RemainingLengthTooHigh(item))?;
    encode_varint(varint, dst).map_err(|_| EncodeError::RemainingLengthTooHigh(item))?;
    Ok(())
}

/// A packet identifier. Two-byte unsigned integer that cannot be zero.
///
/// Ref:
/// - 3.1.1: 2.3.1 Packet Identifier
/// - 5.0:   2.2.1 Packet Identifier
#[derive(Clone, Copy, Eq, Ord, Hash, PartialEq, PartialOrd)]
pub struct PacketIdentifier(NonZeroU16);

impl PacketIdentifier {
    /// The largest value that is a valid packet identifier.
    pub const MAX: Self = Self(NonZeroU16::MAX);

    /// Convert the given raw packet identifier into this type.
    pub fn new(raw: u16) -> Option<Self> {
        Some(Self(NonZeroU16::new(raw)?))
    }

    /// Get the raw packet identifier.
    pub fn get(self) -> u16 {
        self.0.get()
    }

    #[cfg(test)]
    pub fn new_unchecked(raw: u16) -> Self {
        Self::new(raw).unwrap()
    }
}

impl std::fmt::Display for PacketIdentifier {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.0.fmt(f)
    }
}

impl std::fmt::Debug for PacketIdentifier {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        self.0.fmt(f)
    }
}

impl std::ops::Add<u16> for PacketIdentifier {
    type Output = Self;

    fn add(self, other: u16) -> Self::Output {
        let result = self.0.get().wrapping_add(other);
        Self(match NonZeroU16::new(result) {
            Some(result) => result,
            None => NonZeroU16::new(1).expect("NonZeroU16::new(1) must succeed"),
        })
    }
}

impl std::ops::AddAssign<u16> for PacketIdentifier {
    fn add_assign(&mut self, other: u16) {
        *self = *self + other;
    }
}

impl PartialEq<u16> for PacketIdentifier {
    fn eq(&self, other: &u16) -> bool {
        let this: u16 = self.0.into();
        this == *other
    }
}

define_u8_code! {
    /// The level of reliability for a publication
    ///
    /// Ref:
    /// - 3.1.1: 4.3 Quality of Service levels and protocol flows
    /// - 5.0:   4.3 Quality of Service levels and protocol flows
    enum QoS,
    UnrecognizedQoS,
    AtMostOnce = 0x00,
    AtLeastOnce = 0x01,
    ExactlyOnce = 0x02,
}

pub type UserProperty<S> = (ByteStr<S>, ByteStr<S>);
pub type UserProperties<S> = Vec<UserProperty<S>>;

/// A combination of the packet identifier, dup flag and QoS that only allows valid combinations of these three properties.
/// Used in [`Packet::Publish`]
#[allow(clippy::enum_variant_names)]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum PacketIdentifierDupQoS {
    AtMostOnce,
    AtLeastOnce(PacketIdentifier, bool),
    ExactlyOnce(PacketIdentifier, bool),
}

impl PacketIdentifierDupQoS {
    pub fn qos(self) -> QoS {
        let (result, _, _) = self.into();
        result
    }
}

impl From<PacketIdentifierDupQoS> for (QoS, bool, Option<PacketIdentifier>) {
    fn from(value: PacketIdentifierDupQoS) -> Self {
        match value {
            PacketIdentifierDupQoS::AtMostOnce => (QoS::AtMostOnce, false, None),
            PacketIdentifierDupQoS::AtLeastOnce(packet_id, dup) => {
                (QoS::AtLeastOnce, dup, Some(packet_id))
            }
            PacketIdentifierDupQoS::ExactlyOnce(packet_id, dup) => {
                (QoS::ExactlyOnce, dup, Some(packet_id))
            }
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum DecodeError {
    // Common
    #[error("buffer pool error: {0}")]
    BufferPool(
        #[from]
        #[source]
        buffer_pool::Error,
    ),
    #[error("the reserved byte of the CONNECT flags is set")]
    ConnectReservedSet,
    #[error("a zero-length client_id was received without the clean session flag set")]
    ConnectZeroLengthIdWithExistingSession,
    #[error("empty filter")]
    EmptyFilter,
    #[error("empty topic")]
    EmptyTopic,
    #[error("packet is truncated")]
    IncompletePacket,
    #[error("bad string: {0}")]
    InvalidByteStr(&'static str),
    #[error("bad filter {0}")]
    InvalidFilter(String),
    #[error("bad topic {0}")]
    InvalidTopic(String),
    #[error("I/O error: {0}")]
    Io(
        #[from]
        #[source]
        std::io::Error,
    ),
    #[error("expected at least one topic but there were none")]
    NoTopics,
    #[error("PUBLISH packet has DUP flag set and QoS 0")]
    PublishDupAtMostOnce,
    #[error("packet has trailing garbage")]
    TrailingGarbage,
    #[error("could not parse CONNACK flags 0x{0:02X}")]
    UnrecognizedConnAckFlags(u8),
    #[error(
        "unrecognized packet with type 0x{packet_type:02X}, flags 0x{flags:02X}, and remaining length {remaining_length}"
    )]
    UnrecognizedPacket {
        packet_type: u8,
        flags: u8,
        remaining_length: usize,
    },
    #[error("unrecognized protocol name {0}")]
    UnrecognizedProtocolName(String),
    #[error("unrecognized protocol version {0}")]
    UnrecognizedProtocolVersion(u8),
    #[error("unrecognized QoS 0x{0:02X}")]
    UnrecognizedQoS(u8),
    #[error("unrecognized reject kind {0}")]
    UnrecognizedRejectKind(u8),
    #[error("variable byte integer is too large to be decoded")]
    VarintTooHigh,
    #[error(
        "CONNECT packet has will retain and/or will QoS flags set but will flag itself is not set"
    )]
    WillPropertiesFlagsSetButNotWillFlag,
    #[error("packet identifier is 0")]
    ZeroPacketIdentifier,

    #[error("received value must not be 0")]
    ZeroValueOfNonZeroType,

    // Specific to v3
    #[error("CONNECT packet has password flag set but username flag is not set")]
    ConnectPasswordFlagSetButNotUsernameFlag,

    // Specific to v5
    #[error("duplicate property {0}")]
    DuplicateProperty(&'static str),
    #[error("required property {0} is missing")]
    MissingRequiredProperty(&'static str),
    #[error("unexpected property")]
    UnexpectedProperty,
    #[error("unrecognized property identifier 0x{0:02X}")]
    UnrecognizedPropertyIdentifier(u8),

    #[error("maximum packet size set to invalid value {0}")]
    InvalidMaximumPacketSize(u32),
    #[error("receive maximum set to invalid value {0}")]
    InvalidReceiveMaximum(u16),
    #[error("subscription identifier set to invalid value {0}")]
    InvalidSubscriptionIdentifier(u32),
    #[error("topic alias set to invalid value {0}")]
    InvalidTopicAlias(u16),
    #[error("unrecognized authenticate reason code 0x{0:02X}")]
    UnrecognizedAuthenticateReasonCode(u8),
    #[error("unrecognized connect reason code 0x{0:02X}")]
    UnrecognizedConnectReasonCode(u8),
    #[error("unrecognized connect return code 0x{0:02X}")]
    UnrecognizedConnectReturnCode(u8),
    #[error("unrecognized disconnect reason code 0x{0:02X}")]
    UnrecognizedDisconnectReasonCode(u8),
    #[error("unrecognized maximum QoS 0x{0:02X}")]
    UnrecognizedMaximumQoS(u8),
    #[error("unrecognized payload format indicator 0x{0:02X}")]
    UnrecognizedPayloadFormatIndicator(u8),
    #[error("unrecognized puback reason code 0x{0:02X}")]
    UnrecognizedPubAckReasonCode(u8),
    #[error("unrecognized pubcomp reason code 0x{0:02X}")]
    UnrecognizedPubCompReasonCode(u8),
    #[error("unrecognized pubrec reason code 0x{0:02X}")]
    UnrecognizedPubRecReasonCode(u8),
    #[error("unrecognized pubrel reason code 0x{0:02X}")]
    UnrecognizedPubRelReasonCode(u8),
    #[error("unrecognized request problem information 0x{0:02X}")]
    UnrecognizedRequestProblemInformation(u8),
    #[error("unrecognized request response information 0x{0:02X}")]
    UnrecognizedRequestResponseInformation(u8),
    #[error("unrecognized retain available 0x{0:02X}")]
    UnrecognizedRetainAvailable(u8),
    #[error("unrecognized retain handling 0x{0:02X}")]
    UnrecognizedRetainHandling(u8),
    #[error("unrecognized shared subscription available 0x{0:02X}")]
    UnrecognizedSharedSubscriptionAvailable(u8),
    #[error("unrecognized subscribe reason code 0x{0:02X}")]
    UnrecognizedSubscribeReasonCode(u8),
    #[error("unrecognized subscription identifiers available 0x{0:02X}")]
    UnrecognizedSubscriptionIdentifiersAvailable(u8),
    #[error("unrecognized unsubscribe reason code 0x{0:02X}")]
    UnrecognizedUnsubAckReasonCode(u8),
    #[error("unrecognized wildcard subscription available 0x{0:02X}")]
    UnrecognizedWildcardSubscriptionAvailable(u8),
    #[error("the reserved bits of the subscription options are set")]
    SubscriptionOptionsReservedSet,
    #[error("NoLocal bit cannot be set on a Shared Subscription")]
    NoLocalWithSharedSubscription,
}

#[derive(Debug, thiserror::Error)]
pub enum EncodeError {
    // Common
    #[error("insufficient buffer")]
    InsufficientBuffer,
    #[error("I/O error: {0}")]
    Io(
        #[from]
        #[source]
        std::io::Error,
    ),
    #[error("expected at least one topic but there were none")]
    NoTopics,
    #[error("remaining length {0} is too large to be encoded")]
    RemainingLengthTooHigh(usize),
    #[error("string of length {0} is too large to be encoded")]
    StringTooLarge(usize),
    #[error("variable byte integer {0} is too large to be encoded")]
    VarintTooHigh(u32),
    #[error("will payload of length {0} is too large to be encoded")]
    WillTooLarge(usize),

    // Specific to v3
    #[error("CONNECT packet has password but not username")]
    ConnectPasswordWithoutUsername,
    #[error("PUBACK with reason code {0:?} cannot be encoded")]
    NegativePubAck(PubAckReasonCode),
    #[error("PUBCOMP with reason code {0:?} cannot be encoded")]
    NegativePubComp(PubCompReasonCode),
    #[error("PUBREC with reason code {0:?} cannot be encoded")]
    NegativePubRec(PubRecReasonCode),
    #[error("PUBREL with reason code {0:?} cannot be encoded")]
    NegativePubRel(PubRelReasonCode),
    #[error("UNSUBACK with reason code {0:?} cannot be encoded")]
    NegativeUnsubAck(UnsubAckReasonCode),

    // Specific to v5
    #[error("binary data of length {0} is too large to be encoded")]
    BinaryDataTooLarge(usize),
}

/// Decode the fixed header of an MQTT packet.
///
/// This takes a `&mut &[u8]` instead of a `&mut impl Shared` because it's expected to be used
/// in cases where the `Shared` does not necessarily contain a complete packet, and thus needs to keep
/// the partial packet bytes it already has for another attempt after adding more bytes.
///
/// Ref:
/// - 3.1.1: 2 MQTT Control Packet format
/// - 5.0:   2 MQTT Control Packet format
pub fn decode_fixed_header(src: &mut &[u8]) -> Result<Option<(u8, usize)>, DecodeError> {
    let (first_byte, rest) = match src.split_first() {
        Some((first_byte, rest)) => (*first_byte, rest),
        None => return Ok(None),
    };
    *src = rest;

    let Some(remaining_length) = decode_remaining_length(src)? else {
        return Ok(None);
    };

    Ok(Some((first_byte, remaining_length)))
}

/// Metadata about a packet
pub trait PacketMeta<S>: Clone + Sized
where
    S: Shared,
{
    /// The packet type for this kind of packet
    const PACKET_TYPE: u8;

    /// Decodes this packet from the given buffer
    fn decode(flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError>;

    /// Encodes the variable header and payload corresponding to this packet into the given buffer.
    /// The buffer is expected to already have the packet type and body length encoded into it,
    /// and to have reserved enough space to put the bytes of this packet directly into the buffer.
    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>;
}

pub const DOLLAR_SIGN: char = '$';
pub const SEPARATOR: char = '/';
pub const SINGLE_LEVEL_MATCH: char = '+';
pub const SINGLE_LEVEL_MATCH_STR: &str = "+";
pub const MULTI_LEVEL_MATCH: char = '#';
pub const MULTI_LEVEL_MATCH_STR: &str = "#";
pub const SHARED_SUBSCRIPTION_PREFIX: &str = "$share/";

#[cfg(test)]
mod tests {
    use std::{io::IoSlice, slice};

    use bytes::Bytes;
    use matches::assert_matches;

    use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Shared, TestAccumulator};
    use crate::azure_mqtt::mqtt_proto::{
        ByteCounter, ConnAck, Connect, DecodeError, Disconnect, EncodeError, Packet, PingReq,
        PingResp, ProtocolVersion, PubAck, PubComp, PubRec, PubRel, Publication, Publish, SubAck,
        Subscribe, UnsubAck, Unsubscribe,
    };

    use super::{
        DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH, decode_varint, encode_remaining_length,
        encode_varint,
    };

    pub(crate) fn create_packet_as_shared(first_byte: u8, payload: &[u8]) -> Bytes {
        let mut result = TestAccumulator::<Bytes>::with_capacity(5 + payload.len());
        result.try_put_u8(first_byte).unwrap();
        encode_remaining_length(payload.len(), &mut result).unwrap();
        result.try_put_slice(payload).unwrap();
        result.put_done();

        let mut iovec = IoSlice::new(&[]);
        result.to_iovecs(slice::from_mut(&mut iovec));
        Bytes::copy_from_slice(&iovec)
    }

    pub(crate) fn decode<S>(src: &mut S, version: ProtocolVersion) -> Packet<S>
    where
        S: Shared,
    {
        Packet::decode_full(src, version).unwrap()
    }

    pub(crate) fn encode<S>(packet: &Packet<S>, version: ProtocolVersion) -> Bytes
    where
        S: Shared,
    {
        try_encode(packet, version).unwrap()
    }

    pub(crate) fn try_encode<S>(
        packet: &Packet<S>,
        version: ProtocolVersion,
    ) -> Result<Bytes, EncodeError>
    where
        S: Shared,
    {
        let num_bytes_needed = {
            let mut counter = ByteCounter::<_, false>::new();
            packet.encode(&mut counter, version)?;
            counter.into_count()
        };

        let mut accumulator = TestAccumulator::with_capacity(num_bytes_needed);

        // Any validation that failed would've failed when encoding with the ByteCounter above,
        // so this second encode with the BytesAccumulator must succeed.
        packet.encode(&mut accumulator, version).unwrap();

        let mut iovec = IoSlice::new(&[]);
        accumulator.to_iovecs(slice::from_mut(&mut iovec));
        Ok(Bytes::copy_from_slice(&iovec))
    }

    #[test]
    fn varint_encode() {
        varint_encode_inner_ok(0x00, &[0x00]);
        varint_encode_inner_ok(0x01, &[0x01]);

        varint_encode_inner_ok(0x7F, &[0x7F]);
        varint_encode_inner_ok(0x80, &[0x80, 0x01]);
        varint_encode_inner_ok(0x3FFF, &[0xFF, 0x7F]);
        varint_encode_inner_ok(0x4000, &[0x80, 0x80, 0x01]);
        varint_encode_inner_ok(0x001F_FFFF, &[0xFF, 0xFF, 0x7F]);
        varint_encode_inner_ok(0x0020_0000, &[0x80, 0x80, 0x80, 0x01]);
        varint_encode_inner_ok(0x0FFF_FFFF, &[0xFF, 0xFF, 0xFF, 0x7F]);

        varint_encode_inner_too_high(0x1000_0000);
        varint_encode_inner_too_high(0xFFFF_FFFF);

        #[cfg(target_pointer_width = "64")]
        {
            remaining_length_encode_inner_too_high(0xFFFF_FFFF + 1);
            remaining_length_encode_inner_too_high(0xFFFF_FFFF_FFFF_FFFF);
        }
    }

    fn varint_encode_inner_ok(value: u32, expected: &[u8]) {
        let mut iovec = IoSlice::new(&[]);

        // Can't encode into a buffer with no unfilled space left
        let mut bytes = TestAccumulator::<Bytes>::with_capacity(0);
        assert_matches!(
            encode_varint(value, &mut bytes),
            Err(EncodeError::InsufficientBuffer)
        );

        // Can encode into a buffer with unfilled space left and no filled space
        let mut bytes = TestAccumulator::<Bytes>::with_capacity(8);
        encode_varint(value, &mut bytes).unwrap();
        bytes.put_done();
        bytes.to_iovecs(slice::from_mut(&mut iovec));
        assert_eq!(*iovec, *expected);

        // Can encode into a buffer with unfilled space left and some filled space
        let mut bytes = TestAccumulator::<Bytes>::with_capacity(8);
        bytes.try_put_slice(&[0x00; 3][..]).unwrap();
        encode_varint(value, &mut bytes).unwrap();
        bytes.put_done();
        bytes.to_iovecs(slice::from_mut(&mut iovec));
        assert_eq!(iovec[3..], *expected);
    }

    fn varint_encode_inner_too_high(value: u32) {
        let mut bytes = TestAccumulator::<Bytes>::with_capacity(8);
        assert_matches!(encode_varint(value, &mut bytes), Err(EncodeError::VarintTooHigh(v)) if v == value);
    }

    fn remaining_length_encode_inner_too_high(value: usize) {
        let mut bytes = TestAccumulator::<Bytes>::with_capacity(8);
        assert_matches!(encode_remaining_length(value, &mut bytes), Err(EncodeError::RemainingLengthTooHigh(v)) if v == value);
    }

    #[test]
    fn varint_decode() {
        varint_decode_inner_ok(&[0x00], 0x00);
        varint_decode_inner_ok(&[0x01], 0x01);

        varint_decode_inner_ok(&[0x7F], 0x7F);
        varint_decode_inner_ok(&[0x80, 0x01], 0x80);
        varint_decode_inner_ok(&[0xFF, 0x7F], 0x3FFF);
        varint_decode_inner_ok(&[0x80, 0x80, 0x01], 0x4000);
        varint_decode_inner_ok(&[0xFF, 0xFF, 0x7F], 0x001F_FFFF);
        varint_decode_inner_ok(&[0x80, 0x80, 0x80, 0x01], 0x0020_0000);
        varint_decode_inner_ok(&[0xFF, 0xFF, 0xFF, 0x7F], 0x0FFF_FFFF);

        // Longer-than-necessary encodings are not disallowed by the spec
        varint_decode_inner_ok(&[0x81, 0x00], 0x01);
        varint_decode_inner_ok(&[0x81, 0x80, 0x00], 0x01);
        varint_decode_inner_ok(&[0x81, 0x80, 0x80, 0x00], 0x01);

        varint_decode_inner_too_high(&[0x80; DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH]);
        varint_decode_inner_too_high(&[0xFF; DEFAULT_REMAINING_LENGTH_FIELD_MAX_LENGTH]);

        varint_decode_inner_incomplete_packet(&[0x80]);
        varint_decode_inner_incomplete_packet(&[0x80, 0x80]);
        varint_decode_inner_incomplete_packet(&[0x80, 0x80, 0x80]);
    }

    fn varint_decode_inner_ok(mut bytes: &[u8], expected: u32) {
        let actual = decode_varint(&mut bytes).unwrap();
        assert_eq!(actual, Some(expected));
        assert!(bytes.is_empty());
    }

    fn varint_decode_inner_too_high(mut bytes: &[u8]) {
        assert_matches!(decode_varint(&mut bytes), Err(DecodeError::VarintTooHigh));
    }

    fn varint_decode_inner_incomplete_packet(mut bytes: &[u8]) {
        let actual = decode_varint(&mut bytes).unwrap();
        assert_eq!(actual, None);
    }

    /// This test lists all the sizes of the Packet types. This way we don't have to recalculate them
    /// every time we discuss them, and also it helps make sure we think about the impact of any change
    /// we make that causes the sizes to change. For example, this test was added in a change that made
    /// `Connect::will` be a boxed field that reduced the size of the `Connect` type from 352B to 192B,
    /// bringing it close to the two next biggest packets and thus reducing the wasted space in
    /// enums like `Packet`.
    ///
    /// Note that we're testing with the `buffer_pool::bytes` types, not `buffer_pool::tests`,
    /// because we care about the sizes in actual production.
    #[test]
    fn packets_sizes() {
        macro_rules! sizes {
            ([ $($ty:ty = $expected:expr,)* ]) => {
                $(
                    println!("size_of::<{}>() = {}B", stringify!($ty), std::mem::size_of::<$ty>(), );
                )*

                $(
                    assert_eq!(
                        std::mem::size_of::<$ty>(),
                        $expected,
                        "{} does not have the expected size",
                        stringify!($ty),
                    );
                )*
            };
        }

        sizes!([
            ConnAck<Bytes> = 256,
            Connect<Bytes> = 224,
            Disconnect<Bytes> = 104,
            PingReq = 0,
            PingResp = 0,
            PubAck<Bytes> = 64,
            PubComp<Bytes> = 64,
            Publish<Bytes> = 232,
            PubRec<Bytes> = 64,
            PubRel<Bytes> = 64,
            SubAck<Bytes> = 88,
            Subscribe<Bytes> = 64,
            UnsubAck<Bytes> = 88,
            Unsubscribe<Bytes> = 56,

            Packet<Bytes> = 256,

            Publication<Bytes> = 208,
        ]);
    }
}
