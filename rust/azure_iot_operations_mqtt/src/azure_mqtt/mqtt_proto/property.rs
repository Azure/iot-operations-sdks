// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    cmp::Ordering,
    fmt::Display,
    num::{NonZeroU16, NonZeroU32},
    time::Duration,
};

use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Shared};
use crate::azure_mqtt::mqtt_proto::{
    BinaryData, ByteCounter, ByteStr, DecodeError, EncodeError, QoS, SharedExt as _, Topic,
};
use crate::azure_mqtt::mqtt_proto::{
    decode_remaining_length, decode_varint, encode_remaining_length, encode_varint,
};

/// Ref: 3.1.2.10 Keep Alive
/// A Keep Alive value of 0 has the effect of turning off the Keep Alive mechanism.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum KeepAlive {
    Infinite,
    /// Duration in seconds.
    Duration(NonZeroU16),
}

impl From<u16> for KeepAlive {
    fn from(value: u16) -> Self {
        NonZeroU16::new(value).map_or(Self::Infinite, Self::Duration)
    }
}

impl From<KeepAlive> for u16 {
    fn from(value: KeepAlive) -> Self {
        match value {
            KeepAlive::Infinite => 0,
            KeepAlive::Duration(value) => value.into(),
        }
    }
}

impl From<KeepAlive> for Duration {
    fn from(value: KeepAlive) -> Self {
        match value {
            // NOTE: This will be used in timeout checks, so use 100 years
            // as approximation for "infinite".
            KeepAlive::Infinite => Duration::from_secs(100 * 365 * 24 * 60 * 60),
            // Ref: v3 [MQTT-3.1.2-24], v5 [MQTT-3.1.2-22]
            // the actual timeout is 1.5 times Keep-Alive property.
            KeepAlive::Duration(value) => Duration::from_millis(u64::from(value.get()) * 1500),
        }
    }
}

impl PartialOrd<KeepAlive> for KeepAlive {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        match (&self, other) {
            (KeepAlive::Infinite, KeepAlive::Infinite) => Some(Ordering::Equal),
            (KeepAlive::Infinite, KeepAlive::Duration(_)) => Some(Ordering::Greater),
            (KeepAlive::Duration(_), KeepAlive::Infinite) => Some(Ordering::Less),
            (KeepAlive::Duration(this), KeepAlive::Duration(other)) => this.partial_cmp(other),
        }
    }
}

impl Display for KeepAlive {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            KeepAlive::Infinite => write!(f, "infinite"),
            KeepAlive::Duration(value) => write!(f, "{value} seconds"),
        }
    }
}

/// Ref: 3.1.2.11.2 Session Expiry Interval
/// If the Session Expiry Interval is 0xFFFFFFFF, the Session does not expire.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum SessionExpiryInterval {
    Infinite,
    Duration(u32),
}

impl From<u32> for SessionExpiryInterval {
    fn from(value: u32) -> Self {
        match value {
            u32::MAX => Self::Infinite,
            value => Self::Duration(value),
        }
    }
}

impl From<SessionExpiryInterval> for u32 {
    fn from(value: SessionExpiryInterval) -> Self {
        match value {
            SessionExpiryInterval::Infinite => u32::MAX,
            SessionExpiryInterval::Duration(value) => value,
        }
    }
}

impl Ord for SessionExpiryInterval {
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        u32::from(*self).cmp(&u32::from(*other))
    }
}

impl PartialOrd for SessionExpiryInterval {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

macro_rules! generate_enum_and_enum_ref {
    (
        $(
            $(#[ $($meta:meta)* ])*
            $ident:ident ( $($ty:ty),* ),
        )*
    ) => {
        /// Ref: 2.2.2.2 Property
        #[derive(Clone)]
        pub enum Property<S>
        where
            S: Shared,
        {
            $(
                $(#[ $($meta)* ])*
                #[allow(clippy::enum_variant_names)]
                $ident ( $($ty),* ),
            )*
        }

        /// Ref: 2.2.2.2 Property
        pub enum PropertyRef<'a, S>
        where
            S: Shared,
        {
            $(
                $(#[ $($meta)* ])*
                $ident ( $(&'a $ty),* ),
            )*
        }

        // Buggy lint suggests `#[derive]`ing Clone and Copy,
        // but that would insert an `S: Clone` / `S: Copy` bound.
        #[allow(clippy::expl_impl_clone_on_copy)]
        impl<S> Clone for PropertyRef<'_, S> where S: Shared {
            fn clone(&self) -> Self {
                *self
            }
        }

        impl<S> Copy for PropertyRef<'_, S> where S: Shared {}
    };
}

generate_enum_and_enum_ref! {
    /// Ref: 3.2.2.3.7 Assigned Client Identifier
    AssignedClientIdentifier(ByteStr<S>),

    /// Ref: 3.1.2.11.10 Authentication Data
    AuthenticationData(BinaryData<S>),

    /// Ref: 3.1.2.11.9 Authentication Method
    AuthenticationMethod(ByteStr<S>),

    /// Ref: 3.1.2.11.2 Session Expiry Interval (CONNECT)
    ConnectSessionExpiryInterval(SessionExpiryInterval),

    /// Ref: 3.1.3.2.5 Content Type
    ContentType(ByteStr<S>),

    /// Ref: 3.1.3.2.7 Correlation Data
    CorrelationData(BinaryData<S>),

    /// Ref: 3.1.2.11.4 Maximum Packet Size
    MaximumPacketSize(NonZeroU32),

    /// Ref: 3.2.2.3.4 Maximum QoS
    MaximumQoS(QoS),

    /// Ref: 3.1.3.2.4 Message Expiry Interval
    /// Ref: 3.3.2.3.3 Message Expiry Interval
    MessageExpiryInterval(u32),

    /// Ref: 3.1.3.2.3 Payload Format Indicator
    PayloadIsUtf8(bool),

    /// Ref: 3.2.2.3.9 Reason String
    ReasonString(ByteStr<S>),

    /// Ref: 3.1.2.11.3 Receive Maximum
    ReceiveMaximum(NonZeroU16),

    /// Ref: 3.1.2.11.7 Request Problem Information
    RequestProblemInformation(bool),

    /// Ref: 3.1.2.11.6 Request Response Information
    RequestResponseInformation(bool),

    /// Ref: 3.2.2.3.15 Response Information
    ResponseInformation(ByteStr<S>),

    /// Ref: 3.1.3.2.6 Response Topic
    ResponseTopic(Topic<ByteStr<S>>),

    /// Ref: 3.2.2.3.5 Retain Available
    RetainAvailable(bool),

    /// Ref: 3.2.2.3.14 Server Keep Alive
    ServerKeepAlive(KeepAlive),

    /// Ref: 3.2.2.3.16 Server Reference
    ServerReference(ByteStr<S>),

    /// Ref: 3.2.2.3.2 Session Expiry Interval (CONNACK)
    /// Ref: 3.14.2.2.2 Session Expiry Interval (DISCONNECT)
    SessionExpiryInterval(SessionExpiryInterval),

    /// Ref: 3.2.2.3.13 Shared Subscription Available
    SharedSubscriptionAvailable(bool),

    /// Ref: 3.8.2.1.2 Subscription Identifier
    SubscriptionIdentifier(NonZeroU32),

    /// Ref: 3.2.2.3.12 Subscription Identifiers Available
    SubscriptionIdentifiersAvailable(bool),

    /// Ref: 3.3.2.3.4 Topic Alias
    TopicAlias(NonZeroU16),

    /// Ref: 3.1.2.11.5 Topic Alias Maximum
    TopicAliasMaximum(u16),

    /// Ref: 3.1.2.11.8 User Property
    UserProperty(ByteStr<S>, ByteStr<S>),

    /// Ref: 3.2.2.3.11 Wildcard Subscription Available
    WildcardSubscriptionAvailable(bool),

    /// Ref: 3.1.3.2.2 Will Delay Interval
    WillDelayInterval(u32),
}

impl<S> Property<S>
where
    S: Shared,
{
    pub(super) fn decode_all(
        src: &mut S,
    ) -> Result<impl Iterator<Item = Result<Self, DecodeError>>, DecodeError> {
        struct PropertyDecodeIter<S>
        where
            S: Shared,
        {
            src: S,
        }

        impl<S> Iterator for PropertyDecodeIter<S>
        where
            S: Shared,
        {
            type Item = Result<Property<S>, DecodeError>;

            fn next(&mut self) -> Option<Self::Item> {
                if self.src.is_empty() {
                    return None;
                }

                Some(Property::decode(&mut self.src))
            }
        }

        let (remaining_length, remaining_length_len) = {
            let mut src = src.as_ref();
            let original_src_len = src.len();
            let remaining_length =
                decode_remaining_length(&mut src)?.ok_or(DecodeError::IncompletePacket)?;
            let new_src_len = src.len();
            (remaining_length, original_src_len - new_src_len)
        };
        src.drain(remaining_length_len);

        if src.len() < remaining_length {
            return Err(DecodeError::IncompletePacket);
        }
        let src = src.split_to(remaining_length);

        Ok(PropertyDecodeIter { src })
    }

    fn decode(src: &mut S) -> Result<Self, DecodeError> {
        // Note: The spec says property identifiers are technically variable-length integers,
        // but also that all the current defined identifiers are one-byte long,
        // so for now we take the easy route and just parse a byte.
        let identifier = src.try_get_u8()?;

        Ok(match identifier {
            0x01 => {
                let is_utf8 = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => return Err(DecodeError::UnrecognizedPayloadFormatIndicator(value)),
                };
                Self::PayloadIsUtf8(is_utf8)
            }

            0x02 => {
                let interval = src.try_get_u32_be()?;
                Self::MessageExpiryInterval(interval)
            }

            0x03 => {
                let content_type = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::ContentType(content_type)
            }

            0x08 => {
                let response_topic = Topic::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::ResponseTopic(response_topic)
            }

            0x09 => {
                let correlation_data =
                    BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?;
                Self::CorrelationData(correlation_data)
            }

            0x0B => {
                let (varint, varint_len) = {
                    let mut src = src.as_ref();
                    let original_src_len = src.len();
                    let varint = decode_varint(&mut src)?.ok_or(DecodeError::IncompletePacket)?;
                    let new_src_len = src.len();
                    let value = NonZeroU32::new(varint)
                        .ok_or(DecodeError::InvalidSubscriptionIdentifier(varint))?;
                    (value, original_src_len - new_src_len)
                };
                src.drain(varint_len);
                Self::SubscriptionIdentifier(varint)
            }

            0x11 => {
                let interval = SessionExpiryInterval::from(src.try_get_u32_be()?);
                Self::SessionExpiryInterval(interval)
            }

            0x12 => {
                let client_id = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::AssignedClientIdentifier(client_id)
            }

            0x13 => {
                let keep_alive = src.try_get_u16_be()?;
                Self::ServerKeepAlive(keep_alive.into())
            }

            0x15 => {
                let method = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::AuthenticationMethod(method)
            }

            0x16 => {
                let authentication_data =
                    BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?;
                Self::AuthenticationData(authentication_data)
            }

            0x17 => {
                let requested = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => return Err(DecodeError::UnrecognizedRequestProblemInformation(value)),
                };
                Self::RequestProblemInformation(requested)
            }

            0x18 => {
                let interval = src.try_get_u32_be()?;
                Self::WillDelayInterval(interval)
            }

            0x19 => {
                let requested = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => {
                        return Err(DecodeError::UnrecognizedRequestResponseInformation(value));
                    }
                };
                Self::RequestResponseInformation(requested)
            }

            0x1A => {
                let response_information =
                    ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::ResponseInformation(response_information)
            }

            0x1C => {
                let server_reference =
                    ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::ServerReference(server_reference)
            }

            0x1F => {
                let reason_string = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::ReasonString(reason_string)
            }

            0x21 => {
                let value = src.try_get_u16_be()?;
                let value =
                    NonZeroU16::new(value).ok_or(DecodeError::InvalidReceiveMaximum(value))?;
                Self::ReceiveMaximum(value)
            }

            0x22 => {
                let value = src.try_get_u16_be()?;
                Self::TopicAliasMaximum(value)
            }

            0x23 => {
                let value = src.try_get_u16_be()?;
                let value = NonZeroU16::new(value).ok_or(DecodeError::InvalidTopicAlias(value))?;
                Self::TopicAlias(value)
            }

            0x24 => {
                let qos = match src.try_get_u8()? {
                    0x00 => QoS::AtMostOnce,
                    0x01 => QoS::AtLeastOnce,
                    value => return Err(DecodeError::UnrecognizedMaximumQoS(value)),
                };
                Self::MaximumQoS(qos)
            }

            0x25 => {
                let available = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => return Err(DecodeError::UnrecognizedRetainAvailable(value)),
                };
                Self::RetainAvailable(available)
            }

            0x26 => {
                let name = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                let value = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
                Self::UserProperty(name, value)
            }

            0x27 => {
                let value = src.try_get_u32_be()?;
                let value =
                    NonZeroU32::new(value).ok_or(DecodeError::InvalidMaximumPacketSize(value))?;
                Self::MaximumPacketSize(value)
            }

            0x28 => {
                let available = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => {
                        return Err(DecodeError::UnrecognizedWildcardSubscriptionAvailable(
                            value,
                        ));
                    }
                };
                Self::WildcardSubscriptionAvailable(available)
            }

            0x29 => {
                let available = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => {
                        return Err(DecodeError::UnrecognizedSubscriptionIdentifiersAvailable(
                            value,
                        ));
                    }
                };
                Self::SubscriptionIdentifiersAvailable(available)
            }

            0x2A => {
                let available = match src.try_get_u8()? {
                    0x00 => false,
                    0x01 => true,
                    value => {
                        return Err(DecodeError::UnrecognizedSharedSubscriptionAvailable(value));
                    }
                };
                Self::SharedSubscriptionAvailable(available)
            }

            identifier => return Err(DecodeError::UnrecognizedPropertyIdentifier(identifier)),
        })
    }
}

impl<S> PropertyRef<'_, S>
where
    S: Shared,
{
    pub(super) fn encode_all<B, I>(properties: I, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
        I: Iterator<Item = Self> + Clone,
    {
        fn encode_all_inner<'a, B, I>(properties: I, dst: &mut B) -> Result<(), EncodeError>
        where
            B: BytesAccumulator,
            I: Iterator<Item = PropertyRef<'a, B::Shared>> + Clone,
            B::Shared: 'a,
        {
            for property in properties {
                property.encode(dst)?;
            }
            Ok(())
        }

        let properties_length = {
            let mut counter = ByteCounter::<_, true>::new();
            encode_all_inner::<ByteCounter<_, true>, I>(properties.clone(), &mut counter)?;
            counter.into_count()
        };

        encode_remaining_length(properties_length, dst)?;
        encode_all_inner(properties, dst)?;

        Ok(())
    }

    fn encode<B>(self, dst: &mut B) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        match self {
            Self::AssignedClientIdentifier(client_id) => {
                dst.try_put_u8(0x12)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                client_id.encode(dst)?;
            }

            Self::AuthenticationData(authentication_data) => {
                dst.try_put_u8(0x16)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                authentication_data.encode(dst)?;
            }

            Self::AuthenticationMethod(method) => {
                dst.try_put_u8(0x15)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                method.encode(dst)?;
            }

            Self::ConnectSessionExpiryInterval(&interval) => {
                if u32::from(interval) > 0 {
                    PropertyRef::SessionExpiryInterval(&interval).encode(dst)?;
                }
            }

            Self::ContentType(content_type) => {
                dst.try_put_u8(0x03)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                content_type.encode(dst)?;
            }

            Self::CorrelationData(correlation_data) => {
                dst.try_put_u8(0x09)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                correlation_data.encode(dst)?;
            }

            Self::MaximumPacketSize(&value) => {
                let value = value.get();
                if value < u32::MAX {
                    dst.try_put_u8(0x27)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u32_be(value)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::MaximumQoS(&qos) => {
                if let QoS::AtMostOnce | QoS::AtLeastOnce = qos {
                    dst.try_put_u8(0x24)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(qos.into())
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::MessageExpiryInterval(&interval) => {
                dst.try_put_u8(0x02)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                dst.try_put_u32_be(interval)
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }

            Self::PayloadIsUtf8(&is_utf8) => {
                if is_utf8 {
                    dst.try_put_u8(0x01)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x01)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::ReasonString(reason_string) => {
                dst.try_put_u8(0x1F)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                reason_string.encode(dst)?;
            }

            Self::ReceiveMaximum(&value) => {
                let value = value.get();
                if value < u16::MAX {
                    dst.try_put_u8(0x21)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u16_be(value)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::RequestProblemInformation(requested) => {
                if !requested {
                    dst.try_put_u8(0x17)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x00)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::RequestResponseInformation(&requested) => {
                if requested {
                    dst.try_put_u8(0x19)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x01)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::ResponseInformation(response_information) => {
                dst.try_put_u8(0x1A)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                response_information.encode(dst)?;
            }

            Self::ResponseTopic(response_topic) => {
                dst.try_put_u8(0x08)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                response_topic.encode(dst)?;
            }

            Self::RetainAvailable(&available) => {
                if !available {
                    dst.try_put_u8(0x25)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x00)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::ServerKeepAlive(keep_alive) => {
                dst.try_put_u8(0x13)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                dst.try_put_u16_be((*keep_alive).into())
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }

            Self::ServerReference(server_reference) => {
                dst.try_put_u8(0x1C)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                server_reference.encode(dst)?;
            }

            Self::SessionExpiryInterval(&interval) => {
                let interval = u32::from(interval);
                dst.try_put_u8(0x11)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                dst.try_put_u32_be(interval)
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }

            Self::SharedSubscriptionAvailable(&available) => {
                if !available {
                    dst.try_put_u8(0x2A)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x00)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::SubscriptionIdentifier(&varint) => {
                let varint = varint.get();
                dst.try_put_u8(0x0B)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                encode_varint(varint, dst)?;
            }

            Self::SubscriptionIdentifiersAvailable(&available) => {
                if !available {
                    dst.try_put_u8(0x29)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x00)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::TopicAlias(&value) => {
                let value = value.get();
                dst.try_put_u8(0x23)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                dst.try_put_u16_be(value)
                    .ok_or(EncodeError::InsufficientBuffer)?;
            }

            Self::TopicAliasMaximum(&value) => {
                if value > 0 {
                    dst.try_put_u8(0x22)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u16_be(value)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::UserProperty(name, value) => {
                dst.try_put_u8(0x26)
                    .ok_or(EncodeError::InsufficientBuffer)?;
                name.encode(dst)?;
                value.encode(dst)?;
            }

            Self::WildcardSubscriptionAvailable(&available) => {
                if !available {
                    dst.try_put_u8(0x28)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u8(0x00)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }

            Self::WillDelayInterval(&interval) => {
                if interval > 0 {
                    dst.try_put_u8(0x18)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                    dst.try_put_u32_be(interval)
                        .ok_or(EncodeError::InsufficientBuffer)?;
                }
            }
        }

        Ok(())
    }
}

macro_rules! decode_properties {
    (
        @inner
        { $($bindings_decl:tt)* }
        { $($match_body:tt)* }
        { $src:ident }
        { }
    ) => {
        $($bindings_decl)*
        for property in Property::decode_all($src)? {
            match property? {
                $($match_body)*
                // TODO: Include at least the variant name of the unexpected property in the error
                _property => return Err(DecodeError::UnexpectedProperty),
            }
        }
    };

    (
        @inner
        { $($bindings_decl:tt)* }
        { $($match_body:tt)* }
        { $src:ident }
        { $binding:ident : Vec<SubscriptionIdentifier> , $($bindings:tt)* }
    ) => {
        decode_properties! {
            @inner
            {
                $($bindings_decl)*
                let mut $binding = vec![];
            }
            {
                $($match_body)*
                Property::SubscriptionIdentifier(value) => {
                    $binding.push(value);
                },
            }
            { $src }
            { $($bindings)* }
        }
    };

    (
        @inner
        { $($bindings_decl:tt)* }
        { $($match_body:tt)* }
        { $src:ident }
        { $binding:ident : Vec<UserProperty> , $($bindings:tt)* }
    ) => {
        decode_properties! {
            @inner
            {
                $($bindings_decl)*
                let mut $binding = vec![];
            }
            {
                $($match_body)*
                Property::UserProperty(name, value) => {
                    $binding.push((name, value));
                },
            }
            { $src }
            { $($bindings)* }
        }
    };

    (
        @inner
        { $($bindings_decl:tt)* }
        { $($match_body:tt)* }
        { $src:ident }
        { $binding:ident : $variant:ident , $($bindings:tt)* }
    ) => {
        decode_properties! {
            @inner
            {
                $($bindings_decl)*
                let mut $binding = None;
            }
            {
                $($match_body)*
                Property::$variant(value) => {
                    if $binding.replace(value).is_some() {
                        return Err(DecodeError::DuplicateProperty(stringify!($variant)));
                    }
                },
            }
            { $src }
            { $($bindings)* }
        }
    };

    (
        $src:ident,
        $($bindings:tt)*
    ) => {
        decode_properties! {
            @inner
            { }
            { }
            { $src }
            { $($bindings)* }
        }
    };
}

macro_rules! encode_properties {
    (
        @inner
        { $($result:tt)* }
        { $dst:ident }
        { }
    ) => {
        let properties = $($result)*;
        PropertyRef::encode_all::<_, _>(properties, $dst)?;
    };

    (
        @inner
        { $($result:tt)* }
        { $dst:ident }
        { $binding:ident : Vec<SubscriptionIdentifier> , $($bindings:tt)* }
    ) => {
        encode_properties! {
            @inner
            {
                $($result)*
                .chain(
                    $binding.iter()
                    .map(PropertyRef::SubscriptionIdentifier)
                )
            }
            { $dst }
            { $($bindings)* }
        }
    };

    (
        @inner
        { $($result:tt)* }
        { $dst:ident }
        { $binding:ident : Vec<UserProperty> , $($bindings:tt)* }
    ) => {
        encode_properties! {
            @inner
            {
                $($result)*
                .chain(
                    $binding.iter()
                    .map(|(name, value)| PropertyRef::UserProperty(name, value))
                )
            }
            { $dst }
            { $($bindings)* }
        }
    };

    (
        @inner
        { $($result:tt)* }
        { $dst:ident }
        { $binding:ident : Option<$variant:ident> , $($bindings:tt)* }
    ) => {
        encode_properties! {
            @inner
            {
                $($result)*
                .chain($binding.as_ref().map(PropertyRef::$variant))
            }
            { $dst }
            { $($bindings)* }
        }
    };

    (
        @inner
        { $($result:tt)* }
        { $dst:ident }
        { $binding:ident : $variant:ident , $($bindings:tt)* }
    ) => {
        encode_properties! {
            @inner
            {
                $($result)*
                .chain(std::iter::once(PropertyRef::$variant($binding)))
            }
            { $dst }
            { $($bindings)* }
        }
    };

    (
        $dst:ident,
        $($bindings:tt)*
    ) => {
        encode_properties! {
            @inner
            { std::iter::empty() }
            { $dst }
            { $($bindings)* }
        }
    };
}
