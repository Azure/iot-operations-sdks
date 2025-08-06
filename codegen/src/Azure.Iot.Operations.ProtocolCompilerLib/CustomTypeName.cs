namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class CustomTypeName : ITypeName
    {
        public const string Designator = "[CUSTOM]";

        public static CustomTypeName Instance = new();

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, bool local = false)
        {
            if (suffix1 != null)
            {
                return "CustomPayload" + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3);
            }
            else
            {
                return language switch
                {
                    TargetLanguage.Independent => Designator,
                    TargetLanguage.CSharp => "CustomPayload",
                    TargetLanguage.Go => "protocol.Data",
                    TargetLanguage.Rust => "CustomPayload",
                    _ => throw new InvalidOperationException($"There is no {language} representation for {typeof(CustomTypeName)}"),
                };
            }
        }

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null)
        {
            if (suffix1 != null)
            {
                return language switch
                {
                    TargetLanguage.CSharp => "CustomPayload" + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3),
                    TargetLanguage.Go => "custom_payload" + GetSnakeSuffix(suffix1) + GetSnakeSuffix(suffix2) + GetSnakeSuffix(suffix3),
                    TargetLanguage.Rust => "custom_payload" + GetSnakeSuffix(suffix1) + GetSnakeSuffix(suffix2) + GetSnakeSuffix(suffix3),
                    _ => throw new InvalidOperationException($"There is no {language} representation for {typeof(CustomTypeName)}"),
                };
            }
            else
            {
                throw new InvalidOperationException($"{typeof(CustomTypeName)} should not be used for a file name without a suffix");
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
