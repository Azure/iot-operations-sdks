// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SimpleRpcClient
{
    public class PayloadObject
    {
        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }
}
