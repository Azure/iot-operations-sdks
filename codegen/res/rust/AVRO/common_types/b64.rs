/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};

use base64::prelude::*;
use serde::{de, Deserialize, Deserializer, Serialize, Serializer};
use serde_bytes;

#[derive(Clone, Debug)]
pub struct Bytes(pub Vec<u8>);

impl Deref for Bytes {
    type Target = Vec<u8>;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl DerefMut for Bytes {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}

impl Serialize for Bytes {
    fn serialize<S>(&self, s: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        s.serialize_bytes(&self.0)
    }
}

impl<'de> Deserialize<'de> for Bytes {
    fn deserialize<D>(deserializer: D) -> Result<Bytes, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s: Vec<u8> = serde_bytes::deserialize(deserializer)?;
        Ok(Bytes(s))
    }
}
