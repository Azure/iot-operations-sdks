namespace Azure.Iot.Operations.CodeGeneration
{
    using System;

    public class RawTypeName : ITypeName
    {
        public static RawTypeName Instance = new();

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, bool local = false)
        {
            if (suffix1 != null)
            {
                return "RawBytes" + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3) + GetCapitalized(suffix4);
            }
            else
            {
                return language switch
                {
                    TargetLanguage.CSharp => "byte[]",
                    TargetLanguage.Rust => "Vec<u8>",
                    _ => throw new InvalidOperationException($"There is no {language} representation for {typeof(RawTypeName)}"),
                };
            }
        }

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null)
        {
            if (suffix1 != null)
            {
                return language switch
                {
                    TargetLanguage.CSharp => "RawBytes" + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3),
                    TargetLanguage.Rust => "raw_bytes" + GetSnakeSuffix(suffix1) + GetSnakeSuffix(suffix2) + GetSnakeSuffix(suffix3),
                    _ => throw new InvalidOperationException($"There is no {language} representation for {typeof(RawTypeName)}"),
                };
            }
            else
            {
                throw new InvalidOperationException($"{typeof(RawTypeName)} should not be used for a file name without a suffix");
            }
        }

        private static string GetCapitalized(string? suffix)
        {
            return suffix == null ? string.Empty : char.ToUpperInvariant(suffix[0]) + suffix.Substring(1);
        }

        private static string GetSnakeSuffix(string? suffix)
        {
            return suffix == null ? string.Empty : $"_{suffix}";
        }
    }
}
