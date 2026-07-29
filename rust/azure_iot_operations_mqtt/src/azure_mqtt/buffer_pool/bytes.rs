// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! `bytes` implementation of [`BufferPool`].

use bytes::{Bytes, BytesMut};

use crate::azure_mqtt::buffer_pool::{BufferPool, Error};

mod buffers;

#[derive(Clone, Debug, Default)]
pub struct BytesPool;

impl BytesPool {
    pub fn new() -> Self {
        Default::default()
    }
}

impl BufferPool for BytesPool {
    type Shared = Bytes;
    type Owned = BytesMut;

    fn take_owned(&self, len: usize) -> Result<Self::Owned, Error> {
        Ok(BytesMut::with_capacity(len))
    }

    fn take_empty_owned(&self) -> Self::Owned {
        BytesMut::with_capacity(0)
    }
}
