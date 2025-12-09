// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use crate::azure_mqtt::buffer_pool::Shared;
use crate::azure_mqtt::mqtt_proto::{DecodeError, PacketIdentifier};

/// Adds convenience methods to [`crate::azure_mqtt::buffer_pool::Shared`]
pub trait SharedExt {
    fn try_get_u8(&mut self) -> Result<u8, DecodeError>;
    fn try_get_u16_be(&mut self) -> Result<u16, DecodeError>;
    fn try_get_u32_be(&mut self) -> Result<u32, DecodeError>;
    fn try_get_u64_be(&mut self) -> Result<u64, DecodeError>;
    fn try_get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError>;
}

impl<S> SharedExt for S
where
    S: Shared,
{
    fn try_get_u8(&mut self) -> Result<u8, DecodeError> {
        self.get_u8().ok_or(DecodeError::IncompletePacket)
    }

    fn try_get_u16_be(&mut self) -> Result<u16, DecodeError> {
        self.get_u16_be().ok_or(DecodeError::IncompletePacket)
    }

    fn try_get_u32_be(&mut self) -> Result<u32, DecodeError> {
        self.get_u32_be().ok_or(DecodeError::IncompletePacket)
    }

    fn try_get_u64_be(&mut self) -> Result<u64, DecodeError> {
        self.get_u64_be().ok_or(DecodeError::IncompletePacket)
    }

    fn try_get_packet_identifier(&mut self) -> Result<PacketIdentifier, DecodeError> {
        let n = self.try_get_u16_be()?;
        PacketIdentifier::new(n).ok_or(DecodeError::ZeroPacketIdentifier)
    }
}
