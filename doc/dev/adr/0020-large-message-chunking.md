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
       "timeout" : "10000",
       "totalChunks": 5,
       "checksum": "message-hash"
     }
     ```

  - `messageId, chunkIndex, timeout` - present for every chunk; `totalChunks, checksum` - present only for the first chunk. `messageId` is UUID, `timeout` is milliseconds.

**Chunk size calculation**:

- Maximum chunk size will be derived from the MQTT CONNECT packet's Maximum Packet Size.
- A static overhead value will be subtracted from the Maximum Packet Size to account for MQTT packet headers, topic name, user properties, and other metadata.
- The overhead size will be configurable, large enough to simplify calculations while ensuring we stay under the broker's limit.

**Chunk Timeout Mechanism:**

- Set a single timeout period after receiving the first chunk.
- If all chunks aren't received within this window, the message is considered failed.

**Checksum Algorithm Options for MQTT Message Chunking**

SDK will provide user with options to inject their algorithm of choice or use SDK's default SHA-256.

**Implementation layer:**

- Sending Process:
  - When a payload exceeds the maximum packet size, the message is split into fixed-size chunks (with potentially smaller last chunk)
  - Each chunk is sent as a separate MQTT message with the same topic but with chunk metadata.
  - Effort should be made to minimize user properties copied over to every chunk: first chunk will have full set of original user properties and the rest only those that are necessary to reassemble original message (ex.: ```$partition``` property to support shared subscriptions:).
  - QoS settings are maintained across all chunks.
- Receiving Process:
  - The Chunking aware client receives messages and identifies chunked messages by the presence of chunk metadata.
  - Chunks are stored in a temporary buffer, indexed by message ID and chunk index.
  - When all chunks for a message ID are received, they are reassembled in order and message checksum verified.
  - The reconstructed message is then processed as a single message by the application.

### Benefits

- **Property Preservation:** maintains topic, QoS, and other message properties consistently.
- **Network Optimized:** allows efficient transmission of large payloads over constrained networks.

### Implementation Considerations

- **Error Handling:**
  - Chunk timeout mechanisms (see Chunk Timeout Mechanism)
  - Error propagation to application code
- **Performance Optimization:**
  - Concurrent chunk transmission
  - Efficient memory usage during reassembly
- **Security:**
  - Validate message integrity across chunks and prevent chunk injection attacks (see Checksum Algorithm Options for MQTT Message Chunking)