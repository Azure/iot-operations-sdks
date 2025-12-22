// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::io;

use crate::azure_mqtt::buffer_pool::{BufferPool, Owned};
use crate::azure_mqtt::io::ReadableStream;
use crate::azure_mqtt::mqtt_proto;

/// This type wraps a readable network stream and provides API to read from it
/// into a given [`Owned`](buffer_pool::Owned).
pub struct Reader<BP>
where
    BP: BufferPool,
{
    inner: Box<dyn ReadableStream + Send>,
    buf: BP::Owned,
}

#[derive(Debug)]
pub struct RawPacket<S> {
    pub first_byte: u8,
    pub rest: S,
}

impl<BP> Reader<BP>
where
    BP: BufferPool,
{
    pub(crate) fn new(inner: Box<dyn ReadableStream + Send>, buf: BP::Owned) -> Self {
        Self { inner, buf }
    }

    /// Receives and decodes an MQTT packet from the underlying network stream.
    ///
    /// # Errors
    ///
    /// Returns an IO error if the underlying network stream read produced an error,
    /// or an `UnexpectedEof` if 0 bytes were read.
    pub async fn read(&mut self) -> io::Result<RawPacket<BP::Shared>> {
        let (fixed_header_len, first_byte, remaining_length) = loop {
            let mut filled = self.buf.filled();
            let original_filled_len = filled.len();
            if let Some((first_byte, remaining_length)) =
                mqtt_proto::decode_fixed_header(&mut filled).map_err(io::Error::other)?
            {
                let fixed_header_len = original_filled_len - filled.len();
                break (fixed_header_len, first_byte, remaining_length);
            }

            // Reserve space for the largest fixed header, one byte for packet type and four bytes for remaining length.
            self.buf.reserve(5).map_err(io::Error::other)?;
            // ... and read it.
            //
            // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
            unsafe {
                let read = self.inner.read(self.buf.unfilled_mut()).await?;
                self.buf.fill(read);
                if read == 0 {
                    return Err(io::ErrorKind::UnexpectedEof.into());
                }
            }
        };

        if let Some(remaining) =
            (fixed_header_len + remaining_length).checked_sub(self.buf.filled_len())
        {
            self.buf.reserve(remaining).map_err(io::Error::other)?;
        }

        while self.buf.filled_len() < fixed_header_len + remaining_length {
            // SAFETY: Requirements of `unfilled_mut` and `fill` are upheld.
            unsafe {
                let read = self.inner.read(self.buf.unfilled_mut()).await?;
                self.buf.fill(read);
                if read == 0 {
                    return Err(io::ErrorKind::UnexpectedEof.into());
                }
            }
        }

        self.buf.drain(fixed_header_len);
        Ok(RawPacket {
            first_byte,
            rest: self.buf.split_to(remaining_length).freeze(),
        })
    }
}

impl<BP> std::fmt::Debug for Reader<BP>
where
    BP: BufferPool,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Reader").finish_non_exhaustive()
    }
}

#[cfg(test)]
mod tests {
    use std::{
        collections::VecDeque,
        io::{self, Cursor},
        mem::MaybeUninit,
        pin::Pin,
    };

    use matches::assert_matches;

    use crate::azure_mqtt::buffer_pool::{BufferPool as _, BytesPool, maybe_uninit_copy_from_slice};
    use crate::azure_mqtt::io::ReadableStream;

    use super::{RawPacket, Reader};

    impl<R> ReadableStream for Cursor<R>
    where
        R: AsRef<[u8]> + Send,
    {
        fn read<'a>(
            &'a mut self,
            buf: &'a mut [MaybeUninit<u8>],
        ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
            #[allow(clippy::cast_possible_truncation)]
            Box::pin(async move {
                // Would be nice to use `<Cursor as Read>::read()` but that requires `buf: &mut [u8]`

                let pos = self.position() as usize;
                let next_read_buf = &self.get_ref().as_ref()[pos..];
                let read = buf.len().min(next_read_buf.len());
                maybe_uninit_copy_from_slice(&mut buf[..read], &next_read_buf[..read]);
                self.set_position((pos + read) as u64);
                Ok(read)
            })
        }
    }

    struct MockReadableStream {
        reads: VecDeque<Cursor<&'static [u8]>>,
    }

    impl MockReadableStream {
        fn new(reads: impl IntoIterator<Item = &'static [u8]>) -> Self {
            Self {
                reads: reads.into_iter().map(Cursor::new).collect(),
            }
        }
    }

    impl ReadableStream for MockReadableStream {
        fn read<'a>(
            &'a mut self,
            buf: &'a mut [MaybeUninit<u8>],
        ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
            Box::pin(async move {
                let Some(mut next_read) = self.reads.pop_front() else {
                    return Ok(0);
                };
                let read = next_read.read(buf).await?;
                if next_read.position() < next_read.get_ref().len() as u64 {
                    self.reads.push_front(next_read);
                }
                Ok(read)
            })
        }
    }

    impl Drop for MockReadableStream {
        fn drop(&mut self) {
            assert!(
                self.reads.is_empty(),
                "MockReadableStream dropped before all expected reads occurred"
            );
        }
    }

    #[tokio::test]
    async fn it_works() {
        let stream = MockReadableStream::new([
            // Start of CONNECT
            &b"\x10\x12\x00\x04"[..],
            // Rest of CONNECT and start of SUBSCRIBE
            &b"\x4d\x51\x54\x54\x05\x02\x00\x00\x05\x11\xff\xff\xff\xff\x00\x00\x82"[..],
            // Rest of SUBSCRIBE and start of PUBLISH
            &b"\x09\x00\x01\x00\x00\x03\x66\x6f\x6f\x09\x32"[..],
        ]);
        let pool = BytesPool;
        let mut reader = Reader::<BytesPool>::new(Box::new(stream), pool.take_empty_owned());

        let packet = reader.read().await.unwrap();
        assert_matches!(packet, RawPacket { first_byte: 0x10, rest } if rest == b"\x00\x04\x4d\x51\x54\x54\x05\x02\x00\x00\x05\x11\xff\xff\xff\xff\x00\x00"[..]);

        let packet = reader.read().await.unwrap();
        assert_matches!(packet, RawPacket { first_byte: 0x82, rest } if rest == b"\x00\x01\x00\x00\x03\x66\x6f\x6f\x09"[..]);

        assert_matches!(reader.read().await, Err(err) if err.kind() == io::ErrorKind::UnexpectedEof);
    }
}
