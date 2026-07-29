// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use derive_where::derive_where;

use crate::azure_mqtt::buffer_pool::{BytesAccumulator, Shared};
use crate::azure_mqtt::mqtt_proto::{
    Authentication, ByteStr, DecodeError, EncodeError, PacketMeta, Property, PropertyRef,
    ProtocolVersion, SharedExt as _, UserProperties,
};

/// Ref: 3.2 CONNACK – Acknowledge connection request
#[derive(Clone, Debug)]
#[derive_where(Eq, PartialEq)]
pub struct Auth<S>
where
    S: Shared,
{
    pub reason_code: AuthenticateReasonCode,
    // Spec is confusing because it says that authentication method is required but also allows it to be omitted when reason code is omitted,
    // so the net effect is that it's optional.
    pub authentication: Option<Authentication<S>>,
    pub reason_string: Option<ByteStr<S>>,
    pub user_properties: UserProperties<S>,
}

define_u8_code! {
    /// Ref: 3.15.2.1 Authenticate Reason Code
    enum AuthenticateReasonCode,
    UnrecognizedAuthenticateReasonCode,
    Success = 0x00,
    ContinueAuthentication = 0x18,
    ReAuthenticate = 0x19,
}

impl<S> PacketMeta<S> for Auth<S>
where
    S: Shared,
{
    const PACKET_TYPE: u8 = 0xF0;

    fn decode(flags: u8, src: &mut S, version: ProtocolVersion) -> Result<Self, DecodeError> {
        match version {
            ProtocolVersion::V3 => Err(DecodeError::UnrecognizedPacket {
                packet_type: Self::PACKET_TYPE,
                flags,
                remaining_length: src.len(),
            }),

            ProtocolVersion::V5 => match src.try_get_u8() {
                Ok(reason_code) => {
                    let reason_code = reason_code.try_into()?;

                    decode_properties!(
                        src,
                        authentication_method: AuthenticationMethod,
                        authentication_data: AuthenticationData,
                        reason_string: ReasonString,
                        user_properties: Vec<UserProperty>,
                    );

                    Ok(Self {
                        reason_code,
                        authentication: Authentication::of(
                            authentication_method,
                            authentication_data,
                        )?,
                        reason_string,
                        user_properties,
                    })
                }

                Err(DecodeError::IncompletePacket) => Ok(Self {
                    reason_code: AuthenticateReasonCode::Success,
                    authentication: None,
                    reason_string: None,
                    user_properties: vec![],
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
            reason_code,
            authentication,
            reason_string,
            user_properties,
        } = self;

        match version {
            ProtocolVersion::V3 => unreachable!("AUTH packet cannot be encoded for v3"),

            ProtocolVersion::V5 => {
                // The spec allows AUTH packets to not have a variable header if
                // the reason code is success and all other properties are unset.
                // However the MQTTnet client library fails to parse such a packet,
                // so for now we always send a full AUTH packet.
                //
                // Ref: https://github.com/dotnet/MQTTnet/issues/2039

                dst.try_put_u8((*reason_code).into())
                    .ok_or(EncodeError::InsufficientBuffer)?;

                let (authentication_method, authentication_data) =
                    Authentication::into_parts(authentication.as_ref());

                encode_properties! {
                    dst,
                    authentication_method: Option<AuthenticationMethod>,
                    authentication_data: Option<AuthenticationData>,
                    reason_string: Option<ReasonString>,
                    user_properties: Vec<UserProperty>,
                }
            }
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use matches::assert_matches;

    use super::*;
    use crate::azure_mqtt::buffer_pool;
    use crate::azure_mqtt::mqtt_proto::Packet;

    encode_decode_v5! {
        Packet::Auth(Auth {
            reason_code: AuthenticateReasonCode::Success,
            authentication: Some(Authentication { method: "foo".into(), data: Some(b"foo".into()) }),
            reason_string: Some("foo".into()),
            user_properties: vec![("foo".into(), "bar".into())],
        }),

        Packet::Auth(Auth {
            reason_code: AuthenticateReasonCode::ContinueAuthentication,
            authentication: Some(Authentication { method: "foo".into(), data: None }),
            reason_string: None,
            user_properties: vec![],
        }),

        Packet::Auth(Auth {
            reason_code: AuthenticateReasonCode::ReAuthenticate,
            authentication: Some(Authentication { method: "foo".into(), data: None }),
            reason_string: None,
            user_properties: vec![],
        }),

        Packet::Auth(Auth {
            reason_code: AuthenticateReasonCode::ReAuthenticate,
            authentication: None,
            reason_string: Some("foo".into()),
            user_properties: vec![],
        }),
    }

    #[test]
    fn decode_v3_fails() {
        let mut buf = Bytes::from_static(b"\x00\x00");
        assert_matches!(
            Auth::<Bytes>::decode(0, &mut buf, ProtocolVersion::V3),
            Err(DecodeError::UnrecognizedPacket {
                packet_type: 0xF0,
                flags: 0x00,
                remaining_length: 2
            })
        );
    }
}
