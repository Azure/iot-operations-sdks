/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};

use base64::prelude::*;
use bytes;
use serde::{de, Deserialize, Deserializer, Serialize, Serializer};

#[derive(Clone, Debug)]
pub struct Bytes(bytes::Bytes);

impl Deref for Bytes {
    type Target = bytes::Bytes;

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
        s.serialize_str(BASE64_STANDARD.encode(self.as_ref()).as_str())
    }
}

impl<'de> Deserialize<'de> for Bytes {
    fn deserialize<D>(deserializer: D) -> Result<Bytes, D::Error>
    where
        D: Deserializer<'de>,
    {
        let s: String = String::deserialize(deserializer)?;
        Ok(Bytes(bytes::Bytes::from(
            BASE64_STANDARD.decode(&s).map_err(de::Error::custom)?,
        )))
    }
}
