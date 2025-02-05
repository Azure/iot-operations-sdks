namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class CustomTypeName : ITypeName
    {
        public const string Designator = "[CUSTOM]";

        public static CustomTypeName Instance = new();

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
        {
            (null, null, null, TargetLanguage.Independent) => Designator,
            (null, null, null, TargetLanguage.CSharp) => "CustomPayload",
            (null, null, null, TargetLanguage.Go) => "protocol.Data",
            (null, null, null, TargetLanguage.Rust) => "CustomPayload",
            _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(CustomTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(CustomTypeName)}"),
        };

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => throw new InvalidOperationException($"{typeof(CustomTypeName)} should not be used for a file name");
    }
}
