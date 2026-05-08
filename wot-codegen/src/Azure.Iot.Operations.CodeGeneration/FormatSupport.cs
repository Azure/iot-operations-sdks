// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    public static class FormatSupport
    {
        public static string GetSerializerSubNamespace(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => "JSON",
                SerializationFormat.Raw => "raw",
                SerializationFormat.Custom => "custom",
                _ => string.Empty,
            };
        }

        public static string GetSerializerClassName(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => "Utf8JsonSerializer",
                SerializationFormat.Raw => "PassthroughSerializer",
                SerializationFormat.Custom => "ExternalSerializer",
                _ => string.Empty,
            };
        }

        public static EmptyTypeName GetEmptyTypeName(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => EmptyTypeName.JsonInstance,
                SerializationFormat.Raw => EmptyTypeName.RawInstance,
                SerializationFormat.Custom => EmptyTypeName.CustomInstance,
                _ => EmptyTypeName.JsonInstance,
            };
        }
    }
}
