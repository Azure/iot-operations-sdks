# ADR 20: Large Message Chunking in MQTT Protocol

## Status
Proposed

## Context
The MQTT protocol has inherent message size limitations imposed by brokers and network constraints. Azure IoT Operations scenarios often require transmitting payloads that exceed these limits (e.g., firmware updates, large telemetry batches, complex configurations). Without a standardized chunking mechanism, applications must implement their own fragmentation strategies, leading to inconsistent implementations and interoperability issues.

## Decision
We will implement sdk-level message chunking as part of the MQTT layer by using MQTT user properties to carry chunk metadata. This approach will make the chunking mechanism explicit in the SDK rather than hiding it in higher or lower layers.

The chunking mechanism will:
1. Be applied only to MQTT PUBLISH packets
2. Use standardized user properties for chunk metadata:
   - `__chunk`: `<original message id>;<chunk index>;<total chunk count>;<full message check sum>`; `<original message id>,<chunk index>` - present for every chunk; `<total chunk count>,<full message check sum>` - present only for the first chunk.

### Protocol Flow
**Sending Process:**
- When a payload exceeds the maximum packet size, the MQTT client intercepts it before transmission
- The message is split into fixed-size chunks (with potentially smaller last chunk)
- Each chunk is sent as a separate MQTT message with the same topic but with chunk metadata.
- Any user properties and additional metadata not mandated by the MQTT protocol to appear in every message, originally set on the initial PUBLISH packet, will be included only in the first chunk.
- QoS settings are maintained across all chunks.

**Receiving Process:**
   - The MQTT client receives messages and identifies chunked messages by the presence of chunk metadata.
   - Chunks are stored in a temporary buffer, indexed by message ID and chunk index.
   - When all chunks for a message ID are received, they are reassembled in order and message checksum verified.
   - The reconstructed message is then processed as a single message by the application callback.

## Consequences

### Benefits
- **Standards-Based:** Uses existing MQTT features rather than custom transport mechanisms
- **Protocol Transparent:** Makes chunking behavior explicit in the MQTT protocol
- **Property Preservation:** Maintains topic, QoS, and other message properties consistently
- **Network Optimized:** Allows efficient transmission of large payloads over constrained networks

### Implementation Considerations
- **Error Handling:**
  - Chunk timeout mechanisms
  - Error propagation to application code
- **Performance Optimization:**
  - Dynamic chunk sizing based on broker limitations
  - Concurrent chunk transmission
  - Efficient memory usage during reassembly
- **Security:**
  - Validate message integrity across chunks
  - Prevent chunk injection attacks

## Open Questions
1. How do we determine the optimal chunk size? Should it be based on the broker's max size, network conditions, or configurable by the application?
2. Do we create a new API method (`PublishLargeAsync()`) or use the existing `PublishAsync()` API with transparent chunking for oversized payloads?
3. Chunking and shared subscriptions: How do we handle chunked messages across multiple subscribers?
