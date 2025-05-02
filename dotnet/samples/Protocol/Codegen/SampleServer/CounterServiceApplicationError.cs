// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using System.Text.Json.Serialization;

namespace SampleServer
{
    public class CounterServiceApplicationError
    {
        [JsonPropertyName("InvalidRequestArgumentValue")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public int InvalidRequestArgumentValue { get; set; } = default!;
    }
}
