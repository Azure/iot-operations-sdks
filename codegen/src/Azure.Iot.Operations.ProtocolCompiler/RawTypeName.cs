namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class RawTypeName : ITypeName
    {
        public static RawTypeName Instance = new();

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => (suffix1, suffix2, suffix3, language) switch
        {
            (null, null, null, TargetLanguage.Independent) => "",
            (null, null, null, TargetLanguage.CSharp) => "byte[]",
            (null, null, null, TargetLanguage.Go) => "[]byte",
            (null, null, null, TargetLanguage.Rust) => "Vec<u8>",
            _ => throw new InvalidOperationException(suffix1 != null ? $"{typeof(RawTypeName)} cannot take a suffix" : $"There is no {language} representation for {typeof(RawTypeName)}"),
        };

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => throw new InvalidOperationException($"{typeof(RawTypeName)} should not be used for a file name");
    }
}
