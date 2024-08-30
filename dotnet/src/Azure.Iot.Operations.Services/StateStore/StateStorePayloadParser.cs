using System;
using System.Text;
using Azure.Iot.Operations.Services.StateStore.RESP3;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.StateStore
{
    internal class StateStorePayloadParser
    {
        internal static StateStoreSetResponse ParseSetResponse(byte[] payload, HybridLogicalClock? version)
        {
            try
            {
                string status = Resp3Protocol.ParseSimpleString(payload);
                bool isSuccessful = status.Equals("OK", StringComparison.Ordinal);

                if (version == null)
                {
                    throw new StateStoreOperationException($"Received no timestamp metadata from State Store. Received payload: {status}");
                }

                return new StateStoreSetResponse(version, isSuccessful);
            }
            catch (Resp3ProtocolException e)
            {
                if (Resp3Protocol.IsNil(payload))
                {
                    // This case signifies that the set request was not carried out because it was
                    // a conditional set and the condition was not met. Even in this case, the state store
                    // should send back a timestamp.
                    if (version == null)
                    {
                        throw new StateStoreOperationException("Received no timestamp metadata from State Store. Received nil payload");
                    }

                    return new StateStoreSetResponse(version, false);
                }
                else
                { 
                    throw new StateStoreOperationException($"Failed to parse response to \"SET\" request. Response: \"{Encoding.ASCII.GetString(payload)}\"", e);
                }
            }
            catch (Resp3SimpleErrorException e) 
            {
                // This may happen if the user provides an out-of-date fencing token.
                // Users shouldn't have to catch a RESP3 parsing layer exception, so
                // we should wrap it here.
                throw new StateStoreOperationException(e.Message, e);
            }
        }

        internal static byte[]? ParseGetResponse(byte[] payload)
        {
            try
            {
                if (Resp3Protocol.IsNil(payload))
                {
                    return null;
                }

                return Resp3Protocol.ParseBlobString(payload).ToArray();
            }
            catch (Resp3ProtocolException e)
            {
                throw new StateStoreOperationException($"Failed to parse response to \"GET\" request: \"{Encoding.ASCII.GetString(payload)}\"", e);
            }
            catch (Resp3SimpleErrorException e)
            {
                // This may happen if the user provides an out-of-date fencing token.
                // Users shouldn't have to catch a RESP3 parsing layer exception, so
                // we should wrap it here.
                throw new StateStoreOperationException(e.Message, e);
            }
        }

        internal static int ParseDelResponse(byte[] payload)
        {
            try
            {
                return Resp3Protocol.ParseNumber(payload);
            }
            catch (Resp3ProtocolException e)
            {
                throw new StateStoreOperationException($"Failed to parse response to \"DEL\" request: \"{Encoding.ASCII.GetString(payload)}\"", e);
            }
            catch (Resp3SimpleErrorException e)
            {
                // This may happen if the user provides an out-of-date fencing token.
                // Users shouldn't have to catch a RESP3 parsing layer exception, so
                // we should wrap it here.
                throw new StateStoreOperationException(e.Message, e);
            }
        }

        internal static void ValidateKeyNotifyResponse(byte[] payload)
        {
            try
            {
                Resp3Protocol.ThrowIfSimpleError(payload);
                string status = Resp3Protocol.ParseSimpleString(payload);
                if (!status.Equals("OK"))
                {
                    throw new StateStoreOperationException($"Unexpected response to \"KEYNOTIFY\" request. Response: \"{Encoding.ASCII.GetString(payload)}\"");
                }
            }
            catch (Resp3ProtocolException e)
            {
                throw new StateStoreOperationException($"Failed to parse response to \"KEYNOTIFY\" request. Response: \"{Encoding.ASCII.GetString(payload)}\"", e);
            }
        }

        internal static StateStoreKeyNotification ParseKeyNotification(byte[] payload, byte[] keyBeingNotified)
        {
            List<byte[]> blobArrayResponse = Resp3Protocol.ParseBlobArray(payload);

            if (blobArrayResponse.Count < 2)
            {
                throw new StateStoreOperationException("Key notification doesn't contain an expected number of array elements.");
            }

            if (!Encoding.ASCII.GetString(blobArrayResponse[0]).Equals("NOTIFY", StringComparison.Ordinal))
            {
                throw new StateStoreOperationException("Key notification's first segment must be \"NOTIFY\".");
            }
            
            string operationType;
            try
            { 
                operationType = Encoding.ASCII.GetString(blobArrayResponse[1]);
            }
            catch (DecoderFallbackException e) 
            {
                throw new StateStoreOperationException("Key notification's second segment must be ASCII encoded.", e);
            }

            KeyState keyState;
            if (operationType.Equals("SET", StringComparison.Ordinal))
            {
                keyState = KeyState.Updated;

                if (blobArrayResponse.Count != 4)
                {
                    throw new StateStoreOperationException("Key updated notification doesn't contain the expected number of array elements.");
                }

                StateStoreKey key = new StateStoreKey(keyBeingNotified);

                StateStoreValue value = new StateStoreValue(blobArrayResponse[3]);

                return new StateStoreKeyNotification(key, keyState, value);
            }
            else if (operationType.Equals("DELETE", StringComparison.Ordinal))
            {
                keyState = KeyState.Deleted;

                if (blobArrayResponse.Count != 2)
                {
                    throw new StateStoreOperationException("Key deleted notification doesn't contain the expected number of array elements.");
                }

                StateStoreKey key = new StateStoreKey(keyBeingNotified);

                return new StateStoreKeyNotification(key, keyState, null);
            }
            else
            {
                throw new StateStoreOperationException($"Unrecognized key state. Expected \"SET\", \"DELETE\", or \"EXPIRED\" but was {operationType}");
            }
        }

        internal static byte[] BuildSetRequestPayload(StateStoreKey key, StateStoreValue value, StateStoreSetRequestOptions? options = null)
        {
            var builder = new Resp3ArrayBuilder();
            builder.Add(Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("SET")))
                .Add(Resp3Protocol.BuildBlobString(key.Bytes))
                .Add(Resp3Protocol.BuildBlobString(value.Bytes));

            if (options != null)
            {
                if (options.Condition == SetCondition.OnlyIfNotSet)
                {
                    builder.Add(Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("NX")));
                }
                else if (options.Condition == SetCondition.OnlyIfEqualOrNotSet)
                {
                    builder.Add(Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("NEX")));
                }

                if (options.ExpiryTime != null)
                {
                    builder.Add(Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("PX")));
                    builder.Add(Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("" + options.ExpiryTime.Value.TotalMilliseconds)));
                }
            }

            return builder.Build();
        }

        internal static byte[] BuildGetRequestPayload(StateStoreKey key)
        {
            return Resp3Protocol.BuildArray(
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("GET")),
                Resp3Protocol.BuildBlobString(key.Bytes));
        }

        internal static byte[] BuildDelRequestPayload(StateStoreKey key)
        {
            return Resp3Protocol.BuildArray(
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("DEL")),
                Resp3Protocol.BuildBlobString(key.Bytes));
        }

        internal static byte[] BuildVDelRequestPayload(StateStoreKey key, StateStoreValue value)
        {
            return Resp3Protocol.BuildArray(
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("VDEL")),
                Resp3Protocol.BuildBlobString(key.Bytes),
                Resp3Protocol.BuildBlobString(value.Bytes));
        }

        internal static byte[] BuildKeyNotifyRequestPayload(StateStoreKey key)
        {
            return Resp3Protocol.BuildArray(
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("KEYNOTIFY")),
                Resp3Protocol.BuildBlobString(key.Bytes));
        }

        internal static byte[] BuildKeyNotifyStopRequestPayload(StateStoreKey key)
        {
            return Resp3Protocol.BuildArray(
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("KEYNOTIFY")),
                Resp3Protocol.BuildBlobString(key.Bytes),
                Resp3Protocol.BuildBlobString(Encoding.ASCII.GetBytes("STOP")));
        }

    }
}