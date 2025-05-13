# ADR 20: Large Message Chunking in MQTT Protocol

## Status
Proposed

## Context
The MQTT protocol has inherent message size limitations imposed by brokers and network constraints. Azure IoT Operations scenarios often require transmitting payloads that exceed these limits (e.g., firmware updates, large telemetry batches, complex configurations). Without a standardized chunking mechanism, applications must implement their own fragmentation strategies, leading to inconsistent implementations and interoperability issues.

## Decision
We will implement sdk-level message chunking as part of the Protocol layer to transparently handle messages exceeding the MQTT broker's maximum packet size.

**The chunking mechanism will**:
- Be enabled/disabled by a configuration setting.
- Use standardized user properties for chunk metadata:
   - The `__chunk` user property will contain a JSON object with chunking metadata.
   - The JSON structure will include:
     ```json
     {
       "messageId": "unique-id-for-chunked-message",
       "chunkIndex": 0,
       "timeout" : "00:00:10",
       "totalChunks": 5,
       "checksum": "optional-message-hash"
     }
     ```
   - `messageId, chunkIndex, timeout` - present for every chunk; `totalChunks, checksum` - present only for the first chunk.

**Chunk size calculation**:
- Maximum chunk size will be derived from the MQTT CONNECT packet's Maximum Packet Size.
- A static overhead value will be subtracted from the Maximum Packet Size to account for MQTT packet headers, topic name, user properties, and other metadata.
- The overhead size will be configurable, large enough to simplify calculations while ensuring we stay under the broker's limit.

**Implementation layer**:
- Chunking will be implemented as middleware in the Protocol layer between serialization and MQTT client.
 ```
 Application → Protocol Layer (Serialization) → Chunking Middleware → MQTT Client → Broker
 ```
- This makes chunking transparent to application code and compatible with all serialization formats.

- Sending Process:
  - When a payload exceeds the maximum packet size, the midlware intercepts it before transmission
  - The message is split into fixed-size chunks (with potentially smaller last chunk)
  - Each chunk is sent as a separate MQTT message with the same topic but with chunk metadata.
  - Effort should be made to minimize user properties copied over to every chunk: first chunk will have full set of original user properties and the rest only thoses that are neccessary to reassamble original message (ex.: ```$partition``` property to support shared subscriptions:).
  - QoS settings are maintained across all chunks.
- Receiving Process:
  - The Chunking aware client receives messages and identifies chunked messages by the presence of chunk metadata.
  - Chunks are stored in a temporary buffer, indexed by message ID and chunk index.
  - When all chunks for a message ID are received, they are reassembled in order and message checksum verified.
  - The reconstructed message is then processed as a single message by the application.

### Benefits
- **Property Preservation:** Maintains topic, QoS, and other message properties consistently
- **Network Optimized:** Allows efficient transmission of large payloads over constrained networks

### Implementation Considerations
- **Error Handling:**
  - Chunk timeout mechanisms, fixed or sliding timeout window approaches can be used (see Chunk Timeout Mechanism Options in Appendix)
  - Error propagation to application code
- **Performance Optimization:**
  - Concurrent chunk transmission
  - Efficient memory usage during reassembly
- **Security:**
  - Validate message integrity across chunks and prevent chunk injection attacks (covered if checksumm implemented)

# Appendix

## Chunk Timeout Mechanism Options

1. Fixed Timeout Window
   - Set a single timeout period after receiving the first chunk
   - If all chunks aren't received within this window, the message is considered failed
   - **Pros**: Simple implementation, predictable behavior
   - **Cons**: Not adaptive to message size or network conditions

2. Sliding Timeout Window
   - Reset the timeout each time a new chunk arrives
   - Only expire the chunked message if there's a long gap between chunks
   - **Pros**: Tolerates varying network conditions and delivery rates
   - **Cons**: Could keep resources allocated for extended periods