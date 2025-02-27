// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Models
{
    public enum MqttPendingMessagesOverflowStrategy
    {
        DropOldestQueuedMessage,

        DropNewMessage
    }
}
