// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::io::{self, IoSlice};

use crate::azure_mqtt::buffer_pool::{BufferPool, BytesAccumulator, EitherAccumulator, Iovecs};
use crate::azure_mqtt::io::WritableStream;
use crate::azure_mqtt::mqtt_proto::{ByteCounter, Packet, ProtocolVersion};

/// This type wraps a writable network stream and provides API to write data to that stream.
pub struct Writer<BP>
where
    BP: BufferPool,
{
    inner: Box<dyn WritableStream + Send>,
    buf: EitherAccumulator<BP>,
}

impl<BP> Writer<BP>
where
    BP: BufferPool,
{
    pub(crate) fn new(inner: Box<dyn WritableStream + Send>, buf: EitherAccumulator<BP>) -> Self {
        Self { inner, buf }
    }

    /// Encodes an MQTT [`Packet`] and enqueues that data to this `Writer`.
    ///
    /// The bytes of the packet are not guaranteed to be written to the underlying network stream
    /// until `flush` is called.
    ///
    /// # Errors
    ///
    /// Returns an IO error if the underlying network stream write or flush produced an error,
    /// or an `UnexpectedEof` if 0 bytes were written.
    pub async fn write(
        &mut self,
        packet: &Packet<BP::Shared>,
        version: ProtocolVersion,
    ) -> io::Result<()> {
        if !self.buf.can_accept_more() {
            self.flush().await?;
            debug_assert!(
                self.buf.can_accept_more(),
                "Writer cannot accept more even after it was completely drained",
            );
        }

        let num_bytes_needed = {
            let mut counter = ByteCounter::<_, false>::new();
            let () = packet
                .encode(&mut counter, version)
                .map_err(std::io::Error::other)?;
            counter.into_count()
        };

        self.buf
            .reserve(num_bytes_needed)
            .map_err(std::io::Error::other)?;

        let () = packet
            .encode(&mut self.buf, version)
            .map_err(std::io::Error::other)?;

        Ok(())
    }

    /// Ensures that all the data given to previous calls to [`write`] are written to
    /// the underlying network stream.
    ///
    /// # Errors
    ///
    /// Returns an IO error if the underlying network stream write or flush produced an error,
    /// or an `UnexpectedEof` if 0 bytes were written.
    pub async fn flush(&mut self) -> io::Result<()> {
        loop {
            let mut iovecs = [IoSlice::new(&[]); 128];
            let mut written = 0;
            let Iovecs {
                num_iovecs,
                total_len,
            } = self.buf.to_iovecs(&mut iovecs);
            if num_iovecs == 0 {
                break;
            }

            let mut iovecs = &mut iovecs[..num_iovecs];
            while written < total_len {
                match self.inner.write_vectored(iovecs).await {
                    Ok(0) => return Err(io::ErrorKind::UnexpectedEof.into()),
                    Ok(written_) => {
                        written += written_;
                        if written < total_len {
                            IoSlice::advance_slices(&mut iovecs, written_);
                        }
                    }
                    Err(err) => return Err(err),
                }
            }

            self.buf.drain(written);
        }

        () = self.inner.flush().await?;

        Ok(())
    }
}

impl<BP> std::fmt::Debug for Writer<BP>
where
    BP: BufferPool,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Writer").finish_non_exhaustive()
    }
}

#[cfg(test)]
mod tests {
    use std::{
        collections::VecDeque,
        future,
        io::{self, IoSlice},
        ops::Deref,
        pin::Pin,
    };

    use bytes::Bytes;

    use super::Writer;
    use crate::azure_mqtt::buffer_pool::{BufferPool as _, BytesPool, EitherAccumulator};
    use crate::azure_mqtt::io::WritableStream;
    use crate::azure_mqtt::mqtt_proto::{
        Connect, Filter, KeepAlive, Packet, PacketIdentifier, ProtocolVersion, QoS, Subscribe,
        SubscribeOptions, SubscribeTo,
    };

    struct MockWritableStream {
        expected_writes: VecDeque<Vec<&'static [u8]>>,
    }

    impl WritableStream for MockWritableStream {
        fn write_vectored<'a, 'buf>(
            &'a mut self,
            bufs: &'a [IoSlice<'buf>],
        ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
            Box::pin(async move {
                let Some(next_expected_write) = self.expected_writes.pop_front() else {
                    panic!("MockWritableStream did not expect a write of {bufs:02x?}");
                };
                assert!(
                    next_expected_write
                        .iter()
                        .copied()
                        .eq(bufs.iter().map(Deref::deref)),
                    "MockWritableStream did not get expected write:\nexpected {next_expected_write:02x?}\nbut got  {bufs:02x?}",
                );
                Ok(bufs.iter().map(|s| s.len()).sum())
            })
        }

        fn flush(&mut self) -> Pin<Box<dyn Future<Output = io::Result<()>> + Send + '_>> {
            Box::pin(future::ready(Ok(())))
        }
    }

    impl Drop for MockWritableStream {
        fn drop(&mut self) {
            assert!(
                self.expected_writes.is_empty(),
                "MockWritableStream dropped before all expected writes occurred"
            );
        }
    }

    #[tokio::test]
    async fn it_works() {
        let p1 = Packet::<Bytes>::Connect(Connect {
            username: None,
            password: None,
            will: None,
            client_id: None,
            clean_start: true,
            keep_alive: KeepAlive::Infinite,
            other_properties: Default::default(),
        });

        let p2 = Packet::<Bytes>::Subscribe(Subscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![SubscribeTo {
                topic_filter: Filter::new("foo".into()).unwrap(),
                options: SubscribeOptions {
                    maximum_qos: QoS::AtLeastOnce,
                    other_properties: Default::default(),
                },
            }],
            other_properties: Default::default(),
        });

        let stream = MockWritableStream {
            expected_writes: [vec![
                // CONNECT
                &b"\x10\x0d\x00\x04\x4d\x51\x54\x54\x05\x02\x00\x00\x00\x00\x00"[..],
                // Start of SUBSCRIBE
                &b"\x82\x09\x00\x01\x00"[..],
                // SUBSCRIBE topic filter string
                &b"\x00\x03\x66\x6f\x6f"[..],
                // End of SUBSCRIBE
                &b"\x09"[..],
            ]]
            .into(),
        };
        let pool = BytesPool;
        let mut writer = Writer::new(
            Box::new(stream),
            EitherAccumulator::<BytesPool>::Iovecs(pool.take_empty_owned().into()),
        );
        writer.write(&p1, ProtocolVersion::V5).await.unwrap();
        writer.write(&p2, ProtocolVersion::V5).await.unwrap();
        writer.flush().await.unwrap();
    }
}
