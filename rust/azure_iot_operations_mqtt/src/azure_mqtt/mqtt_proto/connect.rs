// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::num::{NonZeroU16, NonZeroU32};

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{self, BytesAccumulator, Owned, Shared};

use crate::azure_mqtt::mqtt_proto::{
    Authentication, BinaryData, ByteStr, DecodeError, EncodeError, PROTOCOL_NAME,
    PROTOCOL_NAME_STR, PROTOCOL_VERSION_V3, PROTOCOL_VERSION_V5, PacketMeta, Property, PropertyRef,
    ProtocolVersion, Publication, PublicationOtherProperties, QoS, SharedExt as _, Topic,
    UserProperties,
    property::{KeepAlive, SessionExpiryInterval},
};

/// Ref: 3.1 CONNECT – Client requests a connection to a Server
#[derive(Clone)]
#[derive_where(Eq, PartialEq)]
pub struct Connect<S>
where
    S: Shared,
{
    pub username: Option<ByteStr<S>>,
    pub password: Option<BinaryData<S>>,
    pub will: Option<Box<(Publication<S>, u32)>>,
    pub client_id: Option<ByteStr<S>>,
    pub clean_start: bool,
    pub keep_alive: KeepAlive,
    pub other_properties: ConnectOtherProperties<S>,
}

#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct ConnectOtherProperties<S>
where
    S: Shared,
{
    pub session_expiry_interval: SessionExpiryInterval,
    pub receive_maximum: NonZeroU16,
    pub maximum_packet_size: NonZeroU32,
    pub topic_alias_maximum: u16,
    pub request_response_information: bool,
    pub request_problem_information: bool,
    pub user_properties: UserProperties<S>,
    pub authentication: Option<Authentication<S>>,
}

impl<S> Connect<S>
where
    S: Shared,
{
    pub fn decode_any_version(
        flags: u8,
        src: &mut S,
    ) -> Result<(Self, ProtocolVersion), DecodeError> {
        let version = decode_start(flags, src)?;
        match version {
            ProtocolVersion::V3 => Ok((Self::decode_rest_v3(src)?, version)),
            ProtocolVersion::V5 => Ok((Self::decode_rest_v5(src)?, version)),
        }
    }

    pub(crate) fn decode_rest_v3(src: &mut S) -> Result<Self, DecodeError> {
        let connect_flags = src.try_get_u8()?;
        if connect_flags & CONNECT_FLAGS_RESERVED != 0 {
            return Err(DecodeError::ConnectReservedSet);
        }
        if connect_flags & (CONNECT_FLAGS_USERNAME | CONNECT_FLAGS_PASSWORD) == 0b0100_0000 {
            return Err(DecodeError::ConnectPasswordFlagSetButNotUsernameFlag);
        }

        let keep_alive = src.try_get_u16_be()?.into();

        let client_id = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
        let client_id = (!client_id.is_empty()).then_some(client_id);
        let clean_start = connect_flags & CONNECT_FLAGS_CLEAN_SESSION != 0;

        if client_id.is_none() && !clean_start {
            return Err(DecodeError::ConnectZeroLengthIdWithExistingSession);
        }

        let will = if connect_flags & CONNECT_FLAGS_WILL == 0 {
            if connect_flags & (CONNECT_FLAGS_WILL_QOS | CONNECT_FLAGS_WILL_RETAIN) != 0 {
                return Err(DecodeError::WillPropertiesFlagsSetButNotWillFlag);
            }

            None
        } else {
            let topic_name = Topic::decode(src)?.ok_or(DecodeError::IncompletePacket)?;

            let qos = match connect_flags & CONNECT_FLAGS_WILL_QOS {
                0x00 => QoS::AtMostOnce,
                0x08 => QoS::AtLeastOnce,
                0x10 => QoS::ExactlyOnce,
                qos => return Err(DecodeError::UnrecognizedQoS(qos >> 3)),
            };

            let retain = connect_flags & CONNECT_FLAGS_WILL_RETAIN != 0;

            let payload = BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?;
            let mut payload = payload.into_shared();
            payload.drain(std::mem::size_of::<u16>());

            Some(Box::new((
                Publication {
                    topic_name,
                    qos,
                    retain,
                    payload,
                    other_properties: PublicationOtherProperties {
                        payload_is_utf8: false,
                        message_expiry_interval: None,
                        response_topic: None,
                        correlation_data: None,
                        user_properties: vec![],
                        content_type: None,
                    },
                },
                0,
            )))
        };

        let username = if connect_flags & CONNECT_FLAGS_USERNAME == 0 {
            None
        } else {
            Some(ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?)
        };

        let password = if connect_flags & CONNECT_FLAGS_PASSWORD == 0 {
            None
        } else {
            Some(BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?)
        };

        let session_expiry_interval = if clean_start {
            SessionExpiryInterval::Duration(0)
        } else {
            SessionExpiryInterval::Infinite
        };

        Ok(Self {
            username,
            password,
            will,
            client_id,
            clean_start,
            keep_alive,
            other_properties: ConnectOtherProperties {
                session_expiry_interval,
                ..Default::default()
            },
        })
    }

    pub(crate) fn decode_rest_v5(src: &mut S) -> Result<Self, DecodeError> {
        let connect_flags = src.try_get_u8()?;
        if connect_flags & CONNECT_FLAGS_RESERVED != 0 {
            return Err(DecodeError::ConnectReservedSet);
        }

        let keep_alive = src.try_get_u16_be()?.into();

        decode_properties!(
            src,
            session_expiry_interval: SessionExpiryInterval,
            receive_maximum: ReceiveMaximum,
            maximum_packet_size: MaximumPacketSize,
            topic_alias_maximum: TopicAliasMaximum,
            request_response_information: RequestResponseInformation,
            request_problem_information: RequestProblemInformation,
            user_properties: Vec<UserProperty>,
            authentication_method: AuthenticationMethod,
            authentication_data: AuthenticationData,
        );

        let client_id = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
        let client_id = (!client_id.is_empty()).then_some(client_id);
        let clean_start = connect_flags & CONNECT_FLAGS_CLEAN_SESSION != 0;

        let will = if connect_flags & CONNECT_FLAGS_WILL == 0 {
            if connect_flags & (CONNECT_FLAGS_WILL_QOS | CONNECT_FLAGS_WILL_RETAIN) != 0 {
                return Err(DecodeError::WillPropertiesFlagsSetButNotWillFlag);
            }

            None
        } else {
            decode_properties!(
                src,
                will_delay_interval: WillDelayInterval,
                will_payload_is_utf8: PayloadIsUtf8,
                will_message_expiry_interval: MessageExpiryInterval,
                will_content_type: ContentType,
                will_response_topic: ResponseTopic,
                will_correlation_data: CorrelationData,
                will_user_properties: Vec<UserProperty>,
            );

            let topic_name = Topic::decode(src)?.ok_or(DecodeError::IncompletePacket)?;

            let qos = match connect_flags & CONNECT_FLAGS_WILL_QOS {
                0x00 => QoS::AtMostOnce,
                0x08 => QoS::AtLeastOnce,
                0x10 => QoS::ExactlyOnce,
                qos => return Err(DecodeError::UnrecognizedQoS(qos >> 3)),
            };

            let retain = connect_flags & CONNECT_FLAGS_WILL_RETAIN != 0;

            let payload = BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?;
            let mut payload = payload.into_shared();
            payload.drain(std::mem::size_of::<u16>());

            Some(Box::new((
                Publication {
                    topic_name,
                    qos,
                    retain,
                    payload,
                    other_properties: PublicationOtherProperties {
                        payload_is_utf8: will_payload_is_utf8.unwrap_or(false),
                        message_expiry_interval: will_message_expiry_interval,
                        response_topic: will_response_topic,
                        correlation_data: will_correlation_data,
                        user_properties: will_user_properties,
                        content_type: will_content_type,
                    },
                },
                will_delay_interval.unwrap_or(0),
            )))
        };

        let username = if connect_flags & CONNECT_FLAGS_USERNAME == 0 {
            None
        } else {
            Some(ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?)
        };

        let password = if connect_flags & CONNECT_FLAGS_PASSWORD == 0 {
            None
        } else {
            Some(BinaryData::decode(src).ok_or(DecodeError::IncompletePacket)?)
        };

        Ok(Self {
            username,
            password,
            will,
            client_id,
            clean_start,
            keep_alive,
            other_properties: ConnectOtherProperties {
                session_expiry_interval: session_expiry_interval
                    .unwrap_or(SessionExpiryInterval::Duration(0)),
                receive_maximum: receive_maximum.unwrap_or(NonZeroU16::MAX),
                maximum_packet_size: maximum_packet_size.unwrap_or(NonZeroU32::MAX),
                topic_alias_maximum: topic_alias_maximum.unwrap_or(0),
                request_response_information: request_response_information.unwrap_or(false),
                request_problem_information: request_problem_information.unwrap_or(true),
                user_properties,
                authentication: Authentication::of(authentication_method, authentication_data)?,
            },
        })
    }

    /// Creates a copy of this `Connect` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(&self, owned: &mut O2) -> Result<Connect<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let username = if let Some(username) = &self.username {
            Some(username.to_shared(owned)?)
        } else {
            None
        };

        let password = if let Some(password) = &self.password {
            Some(password.to_shared(owned)?)
        } else {
            None
        };

        let will = if let Some((will, will_delay_interval)) = self.will.as_deref() {
            Some(Box::new((will.to_shared(owned)?, *will_delay_interval)))
        } else {
            None
        };

        let client_id = if let Some(client_id) = &self.client_id {
            Some(client_id.to_shared(owned)?)
        } else {
            None
        };

        Ok(Connect {
            username,
            password,
            will,
            client_id,
            clean_start: self.clean_start,
            keep_alive: self.keep_alive,
            other_properties: self.other_properties.to_shared(owned)?,
        })
    }
}

impl<S> std::fmt::Debug for Connect<S>
where
    S: Shared,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        // Print all fields except password
        let Self {
            username,
            password: _,
            will,
            client_id,
            clean_start,
            keep_alive,
            other_properties,
        } = self;

        f.debug_struct("Connect")
            .field("username", username)
            .field("will", will)
            .field("client_id", client_id)
            .field("clean_start", clean_start)
            .field("keep_alive", keep_alive)
            .field("other_properties", other_properties)
            .finish()
    }
}

impl<S> PacketMeta<S> for Connect<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0x10;

    fn decode(flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        let actual_version = decode_start(flags, src)?;
        if actual_version != version {
            return Err(DecodeError::UnrecognizedProtocolVersion(
                actual_version.to_u8(),
            ));
        }

        match version {
            ProtocolVersion::V3 => Self::decode_rest_v3(src),
            ProtocolVersion::V5 => Self::decode_rest_v5(src),
        }
    }

    fn encode<B>(&self, dst: &mut B, version: ProtocolVersion) -> Result<(), EncodeError>
    where
        B: BytesAccumulator<Shared = S>,
    {
        let Self {
            username,
            password,
            will,
            client_id,
            clean_start,
            keep_alive,
            other_properties,
        } = self;

        dst.try_put_slice(PROTOCOL_NAME)
            .ok_or(EncodeError::InsufficientBuffer)?;

        dst.try_put_u8(version.to_u8())
            .ok_or(EncodeError::InsufficientBuffer)?;

        {
            if let (ProtocolVersion::V3, None, Some(_)) = (version, &self.username, &self.password)
            {
                return Err(EncodeError::ConnectPasswordWithoutUsername);
            }

            let mut connect_flags = 0b0000_0000_u8;
            if username.is_some() {
                connect_flags |= CONNECT_FLAGS_USERNAME;
            }
            if password.is_some() {
                connect_flags |= CONNECT_FLAGS_PASSWORD;
            }
            if let Some((will, _)) = will.as_deref() {
                connect_flags |= CONNECT_FLAGS_WILL;
                if will.retain {
                    connect_flags |= CONNECT_FLAGS_WILL_RETAIN;
                }
                connect_flags |= u8::from(will.qos) << CONNECT_FLAGS_WILL_QOS.trailing_zeros();
            }
            if *clean_start {
                connect_flags |= CONNECT_FLAGS_CLEAN_SESSION;
            }
            dst.try_put_u8(connect_flags)
                .ok_or(EncodeError::InsufficientBuffer)?;
        }

        dst.try_put_u16_be((*keep_alive).into())
            .ok_or(EncodeError::InsufficientBuffer)?;

        if version.is_v5() {
            let ConnectOtherProperties {
                session_expiry_interval,
                receive_maximum,
                maximum_packet_size,
                topic_alias_maximum,
                request_response_information,
                request_problem_information,
                user_properties,
                authentication,
            } = other_properties;

            let (authentication_method, authentication_data) =
                Authentication::into_parts(authentication.as_ref());

            encode_properties! {
                dst,
                session_expiry_interval: ConnectSessionExpiryInterval,
                receive_maximum: ReceiveMaximum,
                maximum_packet_size: MaximumPacketSize,
                topic_alias_maximum: TopicAliasMaximum,
                request_response_information: RequestResponseInformation,
                request_problem_information: RequestProblemInformation,
                user_properties: Vec<UserProperty>,
                authentication_method: Option<AuthenticationMethod>,
                authentication_data: Option<AuthenticationData>,
            }
        }

        match client_id {
            Some(id) => {
                id.encode(dst)?;
            }
            None => dst
                .try_put_slice(ByteStr::<S>::EMPTY)
                .ok_or(EncodeError::InsufficientBuffer)?,
        }

        if let Some((will, will_delay_interval)) = will.as_deref() {
            let Publication {
                topic_name,
                qos: _,    // Encoded in connect_flags above
                retain: _, // Encoded in connect_flags above
                other_properties:
                    PublicationOtherProperties {
                        payload_is_utf8,
                        message_expiry_interval,
                        response_topic,
                        correlation_data,
                        user_properties,
                        content_type,
                    },
                payload,
            } = will;

            if version.is_v5() {
                encode_properties! {
                    dst,
                    will_delay_interval: WillDelayInterval,
                    payload_is_utf8: PayloadIsUtf8,
                    message_expiry_interval: Option<MessageExpiryInterval>,
                    content_type: Option<ContentType>,
                    response_topic: Option<ResponseTopic>,
                    correlation_data: Option<CorrelationData>,
                    user_properties: Vec<UserProperty>,
                }
            }

            topic_name.encode(dst)?;

            // Encode as a BinaryData, ie with a length prefix.
            let will_len = payload.len();
            dst.try_put_u16_be(
                will_len
                    .try_into()
                    .map_err(|_| EncodeError::WillTooLarge(will_len))?,
            )
            .ok_or(EncodeError::InsufficientBuffer)?;

            dst.put_shared(payload.clone());
        }

        if let Some(username) = username {
            username.encode(dst)?;
        }

        if let Some(password) = password {
            password.encode(dst)?;
        }

        Ok(())
    }
}

impl<S> ConnectOtherProperties<S>
where
    S: Shared,
{
    /// Creates a copy of this `ConnectOtherProperties` with another [`Shared`] type as the backing buffer.
    pub fn to_shared<O2>(
        &self,
        owned: &mut O2,
    ) -> Result<ConnectOtherProperties<O2::Shared>, buffer_pool::Error>
    where
        O2: Owned,
    {
        let mut user_properties = Vec::with_capacity(self.user_properties.len());
        for (key, val) in &self.user_properties {
            let key = key.to_shared(owned)?;
            let val = val.to_shared(owned)?;
            user_properties.push((key, val));
        }

        let authentication = if let Some(authentication) = &self.authentication {
            Some(authentication.to_shared(owned)?)
        } else {
            None
        };

        Ok(ConnectOtherProperties {
            session_expiry_interval: self.session_expiry_interval,
            receive_maximum: self.receive_maximum,
            maximum_packet_size: self.maximum_packet_size,
            topic_alias_maximum: self.topic_alias_maximum,
            request_response_information: self.request_response_information,
            request_problem_information: self.request_problem_information,
            user_properties,
            authentication,
        })
    }
}

const CONNECT_FLAGS_RESERVED: u8 = 0b0000_0001;
const CONNECT_FLAGS_CLEAN_SESSION: u8 = 0b0000_0010;
const CONNECT_FLAGS_WILL: u8 = 0b0000_0100;
const CONNECT_FLAGS_WILL_QOS: u8 = 0b0001_1000;
const CONNECT_FLAGS_WILL_RETAIN: u8 = 0b0010_0000;
const CONNECT_FLAGS_PASSWORD: u8 = 0b0100_0000;
const CONNECT_FLAGS_USERNAME: u8 = 0b1000_0000;

fn decode_start<S>(flags: u8, src: &mut S) -> Result<ProtocolVersion, DecodeError>
where
    S: Shared,
{
    if flags != 0 {
        return Err(DecodeError::UnrecognizedPacket {
            packet_type: 0x10,
            flags,
            remaining_length: src.len(),
        });
    }

    let protocol_name = ByteStr::decode(src)?.ok_or(DecodeError::IncompletePacket)?;
    if protocol_name != PROTOCOL_NAME_STR {
        return Err(DecodeError::UnrecognizedProtocolName(
            protocol_name.as_ref().to_owned(),
        ));
    }

    match src.try_get_u8()? {
        PROTOCOL_VERSION_V3 => Ok(ProtocolVersion::V3),
        PROTOCOL_VERSION_V5 => Ok(ProtocolVersion::V5),
        protocol_version => Err(DecodeError::UnrecognizedProtocolVersion(protocol_version)),
    }
}

impl<S> Default for ConnectOtherProperties<S>
where
    S: Shared,
{
    fn default() -> Self {
        Self {
            session_expiry_interval: SessionExpiryInterval::Duration(0),
            receive_maximum: NonZeroU16::MAX,
            maximum_packet_size: NonZeroU32::MAX,
            topic_alias_maximum: 0,
            request_response_information: false,
            request_problem_information: true,
            user_properties: vec![],
            authentication: None,
        }
    }
}

#[cfg(test)]
mod tests {
    use std::num::{NonZeroU16, NonZeroU32};

    use bytes::Bytes;
    use matches::assert_matches;
    use test_case::test_case;

    use crate::azure_mqtt::buffer_pool::Shared as _;

    use crate::azure_mqtt::mqtt_proto::{
        Authentication, BinaryData, DecodeError, EncodeError, Packet, ProtocolVersion, Publication,
        PublicationOtherProperties, QoS, SessionExpiryInterval, tests, topic,
    };
    use crate::azure_mqtt::mqtt_proto::{Connect, ConnectOtherProperties};

    encode_decode_v3! {
        Packet::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: Some("kittens".into()),
            clean_start: false,
            keep_alive: 30.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Infinite,
                ..Default::default()
            },
        }),

        Packet::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: None,
            clean_start: true,
            keep_alive: 30.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Duration(0),
                ..Default::default()
            },
        }),

        Packet::Connect(Connect {
            username: None,
            password: None,
            will: Some(Box::new((Publication {
                topic_name: topic("foo/bar"),
                qos: QoS::AtLeastOnce,
                retain: true,
                other_properties: PublicationOtherProperties {
                    payload_is_utf8: false,
                    message_expiry_interval: None,
                    response_topic: None,
                    correlation_data: None,
                    user_properties: vec![],
                    content_type: None,
                },
                payload: Bytes::from_static(b"hello world"),
            }, 0))),
            client_id: Some("kittens".into()),
            clean_start: true,
            keep_alive: 30.into(),
            other_properties: Default::default(),
        }),
    }

    encode_decode_v5! {
        Packet::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: None,
            clean_start: true,
            keep_alive: 10.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Duration(0),
                receive_maximum: NonZeroU16::new(11).unwrap(),
                maximum_packet_size: NonZeroU32::new(10).unwrap(),
                topic_alias_maximum: 10,
                request_response_information: true,
                request_problem_information: true,
                user_properties: vec![],
                authentication: None,
            },
        }),

        Packet::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: None,
            clean_start: false,
            keep_alive: 10.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Duration(0),
                receive_maximum: NonZeroU16::new(11).unwrap(),
                maximum_packet_size: NonZeroU32::new(10).unwrap(),
                topic_alias_maximum: 10,
                request_response_information: true,
                request_problem_information: true,
                user_properties: vec![],
                authentication: None,
            },
        }),

        Packet::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: Some("client_id".into()),
            clean_start: true,
            keep_alive: 10.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Duration(0),
                receive_maximum: NonZeroU16::new(11).unwrap(),
                maximum_packet_size: NonZeroU32::new(10).unwrap(),
                topic_alias_maximum: 10,
                request_response_information: true,
                request_problem_information: true,
                user_properties: vec![],
                authentication: None,
            },
        }),

        Packet::Connect(Connect {
            username: Some("username".into()),
            password: Some(b"password".into()),
            will: Some(Box::new((
                Publication {
                    topic_name: topic("topic"),
                    qos: QoS::AtLeastOnce,
                    retain: true,
                    other_properties: PublicationOtherProperties {
                        payload_is_utf8: true,
                        message_expiry_interval: Some(10),
                        response_topic: Some(topic("response")),
                        correlation_data: Some(b"correlation".into()),
                        user_properties: vec![("foo".into(), "bar".into())],
                        content_type: Some("content".into()),
                    },
                    payload: Bytes::from_static(b"payload"),
                },
                10,
            ))),
            client_id: Some("client_id".into()),
            clean_start: false,
            keep_alive: 10.into(),
            other_properties: ConnectOtherProperties {
                session_expiry_interval: SessionExpiryInterval::Duration(30),
                receive_maximum: NonZeroU16::new(11).unwrap(),
                maximum_packet_size: NonZeroU32::new(10).unwrap(),
                topic_alias_maximum: 10,
                request_response_information: true,
                request_problem_information: true,
                user_properties: vec![("foo".into(), "bar".into())],
                authentication: Some(Authentication { method: "method".into(), data: Some(b"data".into()) }),
            },
        }),
    }

    #[test_case(
        b"\x10\x0C\x00\x04MQTT\x04\x22\x00\x0A\x00\x00",
        ProtocolVersion::V3;
        "v3 WILL_RETAIN"
    )]
    #[test_case(
        b"\x10\x0C\x00\x04MQTT\x04\x0A\x00\x0A\x00\x00",
        ProtocolVersion::V3;
        "v3 WILL_QOS"
    )]
    #[test_case(
        b"\x10\x0C\x00\x04MQTT\x04\x2A\x00\x0A\x00\x00",
        ProtocolVersion::V3;
        "v3 WILL_RETAIN and WILL_QOS"
    )]
    #[test_case(
        b"\x10\x0D\x00\x04MQTT\x05\x22\x00\x0A\x00\x00\x00",
        ProtocolVersion::V5;
        "v5 WILL_RETAIN"
    )]
    #[test_case(
        b"\x10\x0D\x00\x04MQTT\x05\x0A\x00\x0A\x00\x00\x00",
        ProtocolVersion::V5;
        "v5 WILL_QOS"
    )]
    #[test_case(
        b"\x10\x0D\x00\x04MQTT\x05\x2A\x00\x0A\x00\x00\x00",
        ProtocolVersion::V5;
        "v5 WILL_RETAIN and WILL_QOS"
    )]
    fn will_properties_flags_set_but_not_will_flag(encoding: &[u8], version: ProtocolVersion) {
        let encoding = BinaryData::<Bytes>::from(encoding);
        let mut buffer = encoding.into_shared();
        buffer.drain(std::mem::size_of::<u16>());

        assert_matches!(
            Packet::decode_full(&mut buffer, version),
            Err(DecodeError::WillPropertiesFlagsSetButNotWillFlag)
        );
    }

    #[test_case(
        b"\x10\x0C\x00\x04MQTT\x04\x02\x00\x0A\x00\x00",
        ProtocolVersion::V3,
        false,
        false
    )]
    #[test_case(
        b"\x10\x16\x00\x04MQTT\x04\x42\x00\x0A\x00\x00\x00\x08password",
        ProtocolVersion::V3,
        false,
        true
    )]
    #[test_case(
        b"\x10\x16\x00\x04MQTT\x04\x82\x00\x0A\x00\x00\x00\x08username",
        ProtocolVersion::V3,
        true,
        false
    )]
    #[test_case(
        b"\x10\x20\x00\x04MQTT\x04\xC2\x00\x0A\x00\x00\x00\x08username\x00\x08password",
        ProtocolVersion::V3,
        true,
        true
    )]
    #[test_case(
        b"\x10\x0D\x00\x04MQTT\x05\x02\x00\x0A\x00\x00\x00",
        ProtocolVersion::V5,
        false,
        false
    )]
    #[test_case(
        b"\x10\x17\x00\x04MQTT\x05\x42\x00\x0A\x00\x00\x00\x00\x08password",
        ProtocolVersion::V5,
        false,
        true
    )]
    #[test_case(
        b"\x10\x17\x00\x04MQTT\x05\x82\x00\x0A\x00\x00\x00\x00\x08username",
        ProtocolVersion::V5,
        true,
        false
    )]
    #[test_case(
        b"\x10\x21\x00\x04MQTT\x05\xC2\x00\x0A\x00\x00\x00\x00\x08username\x00\x08password",
        ProtocolVersion::V5,
        true,
        true
    )]
    // Lint wants `encoding` to be taken as borrow, but that makes the `test_case()` exprs more complicated.
    #[allow(clippy::needless_pass_by_value)]
    fn password_flag_set_but_not_username_flag(
        encoding: &[u8],
        version: ProtocolVersion,
        username_present: bool,
        password_present: bool,
    ) {
        let encoding = BinaryData::<Bytes>::from(encoding);

        // Decode test

        let mut buffer = encoding.clone().into_shared();
        buffer.drain(std::mem::size_of::<u16>());

        let actual_packet = Packet::decode_full(&mut buffer, version);

        if let (ProtocolVersion::V3, false, true) = (version, username_present, password_present) {
            assert_matches!(
                actual_packet,
                Err(DecodeError::ConnectPasswordFlagSetButNotUsernameFlag)
            );
        } else {
            assert_eq!(
                actual_packet.unwrap(),
                Packet::Connect(Connect {
                    username: username_present.then(|| "username".into()),
                    password: password_present.then(|| b"password".into()),
                    will: None,
                    client_id: None,
                    clean_start: true,
                    keep_alive: 10.into(),
                    other_properties: Default::default(),
                })
            );
        }

        // Encode test

        let packet = Packet::Connect(Connect {
            username: username_present.then(|| "username".into()),
            password: password_present.then(|| b"password".into()),
            will: None,
            client_id: None,
            clean_start: true,
            keep_alive: 10.into(),
            other_properties: Default::default(),
        });
        let new_encoding = tests::try_encode(&packet, version);
        if let (ProtocolVersion::V3, false, true) = (version, username_present, password_present) {
            assert_matches!(
                new_encoding,
                Err(EncodeError::ConnectPasswordWithoutUsername)
            );
        } else {
            assert_eq!(new_encoding.unwrap().as_ref(), encoding.as_ref());
        }
    }
}
