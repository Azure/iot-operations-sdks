// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;

    public class JsonTracker
    {
        private string stringValue = string.Empty;
        private double doubleValue = 0.0;
        private ulong uint64Value = 0;
        private long int64Value = 0;
        private Dictionary<string, JsonTracker> objectProperties = new Dictionary<string, JsonTracker>();
        private List<JsonTracker> arrayItems = new List<JsonTracker>();

        public long TokenIndex { get; set; } = -1;

        public JsonValueKind ValueKind { get; set; } = JsonValueKind.Undefined;

        public JsonTracker this[int index]
        {
            get
            {
                if (ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"Cannot index into JSON token of kind {ValueKind}");
                }
                return arrayItems[index];
            }
        }

        public string GetString()
        {
            return stringValue;
        }

        public double GetDouble()
        {
            if (ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"Cannot get double value from JSON token of kind {ValueKind}");
            }
            return doubleValue;
        }

        public ulong GetUInt64()
        {
            if (ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"Cannot get ulong value from JSON token of kind {ValueKind}");
            }
            return uint64Value;
        }

        public long GetInt64()
        {
            if (ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"Cannot get long value from JSON token of kind {ValueKind}");
            }
            return int64Value;
        }

        public bool GetBoolean()
        {
            if (ValueKind != JsonValueKind.True && ValueKind != JsonValueKind.False)
            {
                throw new InvalidOperationException($"Cannot get boolean value from JSON token of kind {ValueKind}");
            }
            return ValueKind == JsonValueKind.True;
        }

        public IEnumerable<KeyValuePair<string, JsonTracker>> EnumerateObject()
        {
            if (ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Cannot enumerate object properties from JSON token of kind {ValueKind}");
            }

            foreach (KeyValuePair<string, JsonTracker> kvp in objectProperties)
            {
                yield return kvp;
            }
        }

        public IEnumerable<JsonTracker> EnumerateArray()
        {
            if (ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Cannot enumerate array items from JSON token of kind {ValueKind}");
            }
            foreach (JsonTracker item in arrayItems)
            {
                yield return item;
            }
        }

        public bool TryGetProperty(string propertyName, [NotNullWhen(true)] out JsonTracker value)
        {
            if (ValueKind == JsonValueKind.Object && objectProperties.TryGetValue(propertyName, out JsonTracker? innerValue))
            {
                value = innerValue;
                return true;
            }
            else
            {
                value = new JsonTracker();
                return false;
            }
        }

        public JsonTracker GetProperty(string propertyName)
        {
            if (!TryGetProperty(propertyName, out JsonTracker value))
            {
                throw new KeyNotFoundException($"Property '{propertyName}' not found in JSON object.");
            }

            return value;
        }

        public int GetArrayLength()
        {
            if (ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Cannot get array length from JSON token of kind {ValueKind}");
            }
            return arrayItems.Count;
        }

        public static JsonTracker Deserialize(ref Utf8JsonReader reader)
        {
            JsonTracker tracker = new JsonTracker
            {
                TokenIndex = reader.TokenStartIndex,
            };

            switch (reader.TokenType)
            {
                case JsonTokenType.None:
                    tracker.ValueKind = JsonValueKind.Undefined;
                    break;
                case JsonTokenType.StartObject:
                    tracker.ValueKind = JsonValueKind.Object;
                    reader.Read();
                    while (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString()!;
                        reader.Read();
                        tracker.objectProperties[propertyName] = Deserialize(ref reader);
                        reader.Read();
                    }
                    break;
                case JsonTokenType.StartArray:
                    tracker.ValueKind = JsonValueKind.Array;
                    reader.Read();
                    while (reader.TokenType != JsonTokenType.EndArray)
                    {
                        tracker.arrayItems.Add(Deserialize(ref reader));
                        reader.Read();
                    }
                    break;
                case JsonTokenType.String:
                    tracker.ValueKind = JsonValueKind.String;
                    tracker.stringValue = reader.GetString() ?? string.Empty;
                    break;
                case JsonTokenType.Number:
                    tracker.ValueKind = JsonValueKind.Number;
                    tracker.doubleValue = reader.GetDouble();
                    if (reader.TryGetUInt64(out ulong uint64Value))
                    {
                        tracker.uint64Value = uint64Value;
                    }
                    if (reader.TryGetInt64(out long int64Value))
                    {
                        tracker.int64Value = int64Value;
                    }
                    break;
                case JsonTokenType.True:
                    tracker.ValueKind = JsonValueKind.True;
                    break;
                case JsonTokenType.False:
                    tracker.ValueKind = JsonValueKind.False;
                    break;
                case JsonTokenType.Null:
                    tracker.ValueKind = JsonValueKind.Null;
                    break;
            }

            return tracker;
        }
    }
}
