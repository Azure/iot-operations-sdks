namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class TypeNamer
    {
        private TypeNameInfo? typeNameInfo;
        private bool suppressTitles;
        private Dictionary<string, string> nameRules;
        private bool capitalizeCaptures;

        public TypeNamer(string? typeNameInfoText)
        {
            this.typeNameInfo = typeNameInfoText != null ? JsonSerializer.Deserialize<TypeNameInfo>(typeNameInfoText) : null;
            this.suppressTitles = this.typeNameInfo?.SuppressTitles ?? false;
            this.nameRules = this.typeNameInfo?.NameRules ?? new();
            this.capitalizeCaptures = this.typeNameInfo?.CapitalizeCaptures ?? false;
        }

        public string GenerateTypeName(string schemaName, string? keyName, string? title)
        {
            if (title != null && !this.suppressTitles)
            {
                return title;
            }

            if (keyName == null)
            {
                string pathlessName = Path.GetFileName(schemaName);
                int dotIx = pathlessName.IndexOf('.');
                string unsuffixedName = dotIx < 0 ? pathlessName : pathlessName.Substring(0, dotIx);
                return Capitalize(unsuffixedName);
            }

            foreach (KeyValuePair<string, string> rule in this.nameRules)
            {
                Regex rx = new(rule.Key, RegexOptions.IgnoreCase);
                Match? match = rx.Match(keyName);
                if (match.Success)
                {
                    string replacement = rule.Value;
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        string captureValue = match.Groups[i].Captures[0].Value;
                        replacement = replacement.Replace($"{{{i}}}", this.capitalizeCaptures ? Capitalize(captureValue) : captureValue);
                    }

                    return replacement;
                }
            }

            uint keyHash = (uint)((long)keyName.GetHashCode() - (long)int.MinValue);
            return $"Type{keyHash:D10}";
        }

        private static string Capitalize(string input) => input.Length == 0 ? input : char.ToUpper(input[0]) + input.Substring(1);
    }
}
