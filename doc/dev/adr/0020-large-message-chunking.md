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
       "totalChunks": 5,
       "checksum": "message-hash"
     }
     ```

  - `messageId, chunkIndex` - present for every chunk; `totalChunks, checksum` - present only for the first chunk. `messageId` is UUID.

**Chunk size calculation:**

- Maximum chunk size will be derived from the MQTT CONNECT packet's Maximum Packet Size.
- A static overhead value will be subtracted from the Maximum Packet Size to account for MQTT packet headers, topic name, user properties, and other metadata.
- The overhead size will be configurable, large enough to simplify calculations while ensuring we stay under the broker's limit.

**Chunk Timeout Mechanism**

> [MQTT-3.3.2-6] | The PUBLISH packet sent to a Client by the Server MUST contain a Message Expiry Interval set to the received value minus the time that the message has been waiting in the Server.

The receiving client uses the Message Expiry Interval from the first chunk as the timeout period for collecting all remaining chunks of the message.
**Edge case:** Since the Message Expiry Interval is specified in seconds, chunked messages may behave differently than single messages when the expiry interval is very short (e.g., 1 second remaining). For a single large message, the QoS flow would complete even if the expiry interval expires during transmission. However, with chunking, if the remaining expiry interval is too short to receive all chunks, the message reassembly will fail due to timeout.

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
  - When all chunks for a message ID are received, they are reassembled in order and message checksum verified (see Checksum Algorithm Options for MQTT Message Chunking).
  - The reconstructed message is then processed as a single message by the application.

### Implementation Considerations

- **Error Handling:**
  - Chunk timeout mechanisms (see Chunk Timeout Mechanism)
  - Error propagation to application code
- **Performance Optimization:**
  - Concurrent chunk transmission
  - Efficient memory usage during reassembly
  - Maximum reassembly buffer size limits (configurable)

### Benefits

- **Property Preservation:** maintains topic, QoS, and other message properties consistently.
- **Network Optimized:** allows efficient transmission of large payloads over constrained networks.

### Compatibility

- Non-chunking-aware clients will receive individual chunks as separate messages.

## Appendix

### Message Flow Diagram

```mermaid
sequenceDiagram
    participant Sender as Sending Client
    participant Broker as MQTT Broker
    participant Receiver as Receiving Client

    Note over Sender: Large message (>max size)
    Sender->>Sender: Split into chunks
    Note right of Sender: Calculate chunk size:<br/>MaxPacketSize - Overhead

    loop For each chunk
        Sender->>Broker: MQTT PUBLISH with __chunk metadata
        Note over Broker: No special handling<br/>required by broker
        Broker->>Receiver: Forward chunk
        Note over Receiver: First chucnk starts timeout count down
        Receiver->>Receiver: Store in buffer
        Note left of Receiver: Index by:<br/>messageId + chunkIndex
    end

    Note over Receiver: All chunks received
    Receiver->>Receiver: Verify checksum
    Note right of Receiver: SHA-256 or<br/>custom algorithm
    Receiver->>Receiver: Reassemble message
    Note over Receiver: Process complete message

    rect rgba(255, 0, 0, 0.1)
        Note over Sender,Receiver: Error Path
        Receiver-->>Receiver: Timeout occurred
        Receiver-->>Receiver: Cleanup buffers
    end
```
