namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using DTDLParser;

    public class CodeName : ITypeName, IEquatable<CodeName>
    {
        private readonly string givenName;
        private readonly string[] components;
        private readonly string lowerName;
        private readonly string pascalName;
        private readonly string camelName;
        private readonly string snakeName;

        public CodeName(Dtmi dtmi)
            : this(NameFromDtmi(dtmi))
        {
        }

        public CodeName(string givenName = "")
            : this(givenName, Decompose(givenName))
        {
        }

        public CodeName(string baseName, string suffix1, string? suffix2 = null)
            : this(Extend(baseName, suffix1, suffix2), DecomposeAndExtend(baseName, suffix1, suffix2))
        {
        }

        public CodeName(CodeName baseName, string suffix1)
            : this(Extend(baseName.AsGiven, suffix1, null), baseName.AsComponents.Append(suffix1))
        {
        }

        public CodeName(CodeName baseName, string suffix1, string suffix2)
            : this(Extend(baseName.AsGiven, suffix1, suffix2), baseName.AsComponents.Append(suffix1).Append(suffix2))
        {
        }

        public CodeName(string givenName, IEnumerable<string> components)
        {
            this.givenName = givenName;
            this.components = components.ToArray();
            lowerName = string.Concat(components);
            pascalName = string.Concat(components.Select(c => char.ToUpper(c[0]) + c.Substring(1)));
            camelName = pascalName.Length > 0 ? char.ToLower(pascalName[0]) + pascalName.Substring(1) : string.Empty;
            snakeName = string.Join('_', components);
        }

        public override string ToString()
        {
            throw new Exception($"ToString() called on CodeName({AsGiven})");
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CodeName);
        }

        public bool Equals(CodeName? other)
        {
            return !ReferenceEquals(null, other) && AsGiven == other.AsGiven;
        }

        public override int GetHashCode()
        {
            return AsGiven.GetHashCode();
        }

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => language switch
        {
            TargetLanguage.Independent => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.CSharp => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.Go => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.Rust => AsPascal(suffix1, suffix2, suffix3),
            _ => AsPascal(suffix1, suffix2, suffix3),
        };

        public string GetFieldName(TargetLanguage language) => language switch
        {
            TargetLanguage.Independent => AsGiven,
            TargetLanguage.CSharp => AsPascal(),
            TargetLanguage.Go => AsPascal(),
            TargetLanguage.Rust => AsSnake(),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetMethodName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null) => language switch
        {
            TargetLanguage.CSharp => AsPascal(suffix1, suffix2, suffix3, prefix),
            TargetLanguage.Go => AsPascal(suffix1, suffix2, suffix3, prefix),
            TargetLanguage.Rust => AsSnake(suffix1, suffix2, suffix3, prefix),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetVariableName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null) => language switch
        {
            TargetLanguage.CSharp => AsCamel(suffix1, suffix2, suffix3, prefix),
            TargetLanguage.Go => AsCamel(suffix1, suffix2, suffix3, prefix),
            TargetLanguage.Rust => AsSnake(suffix1, suffix2, suffix3, prefix),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => language switch
        {
            TargetLanguage.Independent => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.CSharp => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.Go => AsSnake(suffix1, suffix2, suffix3),
            TargetLanguage.Rust => AsSnake(suffix1, suffix2, suffix3),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetFolderName(TargetLanguage language) => language switch
        {
            TargetLanguage.Independent => AsPascal(),
            TargetLanguage.CSharp => AsPascal(),
            TargetLanguage.Go => AsLower(),
            TargetLanguage.Rust => AsSnake(),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public bool IsEmpty => string.IsNullOrEmpty(givenName);

        public string AsGiven => givenName;

        private string[] AsComponents => components;

        private string AsLower(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null)
        {
            return prefix ?? string.Empty + lowerName + suffix1 ?? string.Empty + suffix2 ?? string.Empty + suffix3 ?? string.Empty;
        }

        private string AsPascal(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null)
        {
            return GetCapitalized(prefix) + pascalName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3);
        }

        private string AsCamel(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix + pascalName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3);
            }
            else if (!string.IsNullOrEmpty(givenName))
            {
                return camelName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3);
            }
            else
            {
                return suffix1 + GetCapitalized(suffix2) + GetCapitalized(suffix3);
            }
        }

        private string AsSnake(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null)
        {
            return GetSnakePrefix(prefix) + snakeName + GetSnakeSuffix(suffix1) + GetSnakeSuffix(suffix2) + GetSnakeSuffix(suffix3);
        }

        private static string Extend(string baseName, string suffix1, string? suffix2)
        {
            bool snakeWise = baseName.Contains('_');
            StringBuilder givenName = new(baseName);
            givenName.Append(Extension(suffix1, snakeWise));
            if (suffix2 != null)
            {
                givenName.Append(Extension(suffix2, snakeWise));
            }
            return givenName.ToString();
        }

        private static List<string> DecomposeAndExtend(string baseName, string suffix1, string? suffix2)
        {
            List<string> components = Decompose(baseName);
            components.Add(suffix1);
            if (suffix2 != null)
            {
                components.Add(suffix2);
            }
            return components;
        }

        private static List<string> Decompose(string givenName)
        {
            List<string>  components = new();
            StringBuilder stringBuilder = new();
            char p = '\0';

            foreach (char c in givenName)
            {
                if (((char.IsUpper(c) && char.IsLower(p)) || c == '_') && stringBuilder.Length > 0)
                {
                    components.Add(stringBuilder.ToString());
                    stringBuilder.Clear();
                }

                if (c != '_')
                {
                    stringBuilder.Append(char.ToLower(c));
                }

                p = c;
            }

            if (stringBuilder.Length > 0)
            {
                components.Add(stringBuilder.ToString());
            }

            return components;
        }

        private static string NameFromDtmi(Dtmi dtmi, int index = -1)
        {
            if (index < 0)
            {
                index = dtmi.Labels.Length - 1;
            }

            string lastLabel = dtmi.Labels[index];
            string prefix = !lastLabel.StartsWith("_") || lastLabel.StartsWith("__") ? string.Empty : NameFromDtmi(dtmi, index - 1);
            return prefix + GetCapitalized(lastLabel.TrimStart('_'));
        }

        private static string Extension(string suffix, bool snakeWise)
        {
            return snakeWise ? GetSnakeSuffix(suffix) : GetCapitalized(suffix);
        }

        private static string GetCapitalized(string? suffix)
        {
            return suffix == null ? string.Empty : char.ToUpperInvariant(suffix[0]) + suffix.Substring(1);
        }

        private static string GetSnakeSuffix(string? suffix)
        {
            return suffix == null ? string.Empty : $"_{suffix}";
        }

        private static string GetSnakePrefix(string? prefix)
        {
            return prefix == null ? string.Empty : $"{prefix}_";
        }
    }
}
