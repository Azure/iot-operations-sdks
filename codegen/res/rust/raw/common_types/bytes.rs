/* This file will be copied into the folder for generated code. */

use std::ops::{Deref, DerefMut};
use bytes;

#[derive(Clone, Debug)]
pub struct Bytes (pub bytes::Bytes);

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
