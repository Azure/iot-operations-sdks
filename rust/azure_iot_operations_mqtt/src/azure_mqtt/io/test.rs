// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::future::{self, Future};
use std::io::{self, IoSlice};
use std::mem::MaybeUninit;
use std::pin::Pin;

use tokio::sync::mpsc::{UnboundedReceiver, UnboundedSender};

use crate::azure_mqtt::buffer_pool::{
    BufferPool, BytesAccumulator as _, EitherAccumulator, Owned as _, Shared,
};
use crate::azure_mqtt::io::{ReadableStream, Reader, WritableStream, Writer, reader::RawPacket};
use crate::azure_mqtt::mqtt_proto::{self, ByteCounter, Packet, ProtocolVersion};

pub fn connect<BP>(
    incoming_packets: UnboundedReceiver<Packet<BP::Shared>>,
    outgoing_packets: UnboundedSender<Packet<BP::Shared>>,
    reader_pool: &BP,
    writer_pool: &BP,
) -> (Reader<BP>, Writer<BP>)
where
    BP: BufferPool,
{
    let read_buf = reader_pool.take_empty_owned();
    let reader = Reader::new(
        Box::new(TestStreamRead {
            incoming_packets,
            buffer_pool: reader_pool.clone(),
            current_packet: None,
        }),
        read_buf,
    );

    let write_buf = EitherAccumulator::Single(Default::default());
    let writer = Writer::new(
        Box::new(TestStreamWrite {
            outgoing_packets,
            buffer_pool: writer_pool.clone(),
            current_buf: writer_pool.take_empty_owned(),
        }),
        write_buf,
    );

    (reader, writer)
}

struct TestStreamRead<BP>
where
    BP: BufferPool,
{
    incoming_packets: UnboundedReceiver<Packet<BP::Shared>>,
    buffer_pool: BP,
    current_packet: Option<BP::Shared>,
}

impl<BP> ReadableStream for TestStreamRead<BP>
where
    BP: BufferPool,
{
    fn read<'a>(
        &'a mut self,
        buf: &'a mut [MaybeUninit<u8>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        Box::pin(async move {
            loop {
                if let Some(current_packet) = &mut self.current_packet {
                    let read = buf.len().min(current_packet.len());
                    let to_read_from = current_packet.split_to(read);
                    crate::azure_mqtt::buffer_pool::maybe_uninit_copy_from_slice(
                        &mut buf[..read],
                        to_read_from.as_ref(),
                    );
                    if current_packet.is_empty() {
                        self.current_packet = None;
                    }
                    return Ok(read);
                }

                let Some(packet) = self.incoming_packets.recv().await else {
                    // Sender dropped == EOF
                    return Ok(0);
                };
                let packet = {
                    let num_bytes_needed = {
                        let mut counter = ByteCounter::<_, false>::new();
                        packet.encode(&mut counter, ProtocolVersion::V5).unwrap();
                        counter.into_count()
                    };

                    let mut accumulator = EitherAccumulator::<BP>::Single(Default::default());
                    accumulator.reserve(num_bytes_needed).unwrap();

                    packet
                        .encode(&mut accumulator, ProtocolVersion::V5)
                        .unwrap();

                    let mut iovec = IoSlice::new(&[]);
                    accumulator.to_iovecs(std::slice::from_mut(&mut iovec));

                    let mut write_buf = self.buffer_pool.take_empty_owned();
                    Shared::new(&mut write_buf, &iovec).unwrap()
                };
                self.current_packet = Some(packet);
            }
        })
    }
}

pub struct TestStreamWrite<BP>
where
    BP: BufferPool,
{
    outgoing_packets: UnboundedSender<Packet<BP::Shared>>,
    buffer_pool: BP,
    current_buf: BP::Owned,
}

impl<BP> WritableStream for TestStreamWrite<BP>
where
    BP: BufferPool,
{
    fn write_vectored<'a, 'buf>(
        &'a mut self,
        bufs: &'a [IoSlice<'buf>],
    ) -> Pin<Box<dyn Future<Output = io::Result<usize>> + Send + 'a>> {
        Box::pin(async move {
            let mut written = 0;
            for buf in bufs {
                self.current_buf.reserve(buf.len()).unwrap();
                self.current_buf.put_slice(buf);
                written += buf.len();
            }

            loop {
                let (fixed_header_len, first_byte, remaining_length) = {
                    let mut filled = self.current_buf.filled();
                    let original_filled_len = filled.len();
                    if let Some((first_byte, remaining_length)) =
                        mqtt_proto::decode_fixed_header(&mut filled).unwrap()
                    {
                        let fixed_header_len = original_filled_len - filled.len();
                        (fixed_header_len, first_byte, remaining_length)
                    } else {
                        break;
                    }
                };

                if self.current_buf.filled_len() < fixed_header_len + remaining_length {
                    break;
                }

                self.current_buf.drain(fixed_header_len);
                let mut raw_packet = RawPacket {
                    first_byte,
                    rest: self.current_buf.split_to(remaining_length).freeze(),
                };
                let packet = Packet::decode(
                    raw_packet.first_byte,
                    &mut raw_packet.rest,
                    ProtocolVersion::V5,
                )
                .unwrap();
                if self.outgoing_packets.send(packet).is_err() {
                    // Receiver dropped == EOF
                    return Ok(0);
                }
            }

            Ok(written)
        })
    }

    fn flush(&mut self) -> Pin<Box<dyn Future<Output = io::Result<()>> + Send + '_>> {
        Box::pin(future::ready(Ok(())))
    }
}
