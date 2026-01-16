namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Diagnostics.CodeAnalysis;
    using Azure.Iot.Operations.CodeGeneration;

    internal static class EnvoyGeneratorSupport
    {
        [return: NotNullIfNotNull(nameof(schemaType))]
        internal static ITypeName? GetTypeName(string? schemaType, SerializationFormat format)
        {
            if (schemaType == null)
            {
                return null;
            }

            return format switch
            {
                SerializationFormat.Raw => RawTypeName.Instance,
                SerializationFormat.Custom => CustomTypeName.Instance,
                _ => new CodeName(schemaType),
            };
        }
    }
}
