namespace Azure.Iot.Operations.CodeGeneration
{
    public static class FormatSupport
    {
        public static string GetSerializerSubNamespace(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => "JSON",
                _ => string.Empty,
            };
        }

        public static string GetSerializerClassName(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => "Utf8JsonSerializer",
                _ => string.Empty,
            };
        }

        public static EmptyTypeName GetEmptyTypeName(this SerializationFormat format)
        {
            return format switch
            {
                SerializationFormat.Json => EmptyTypeName.JsonInstance,
                _ => EmptyTypeName.JsonInstance,
            };
        }
    }
}
