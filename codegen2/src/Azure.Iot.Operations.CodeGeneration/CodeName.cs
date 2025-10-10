namespace Azure.Iot.Operations.CodeGeneration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class CodeName : ITypeName, IEquatable<CodeName>
    {
        private readonly string givenName;
        private readonly string[] components;
        private readonly string lowerName;
        private readonly string pascalName;
        private readonly string camelName;
        private readonly string snakeName;

        public CodeName(string givenName = "")
            : this(givenName, Decompose(givenName))
        {
        }

        public CodeName(string baseName, string suffix1, string? suffix2 = null, string? suffix3 = null)
            : this(Extend(baseName, suffix1, suffix2, suffix3), DecomposeAndExtend(baseName, suffix1, suffix2, suffix3))
        {
        }

        public CodeName(CodeName baseName, string suffix1)
            : this(Extend(baseName.AsGiven, suffix1, null, null), baseName.AsComponents.Append(suffix1))
        {
        }

        public CodeName(CodeName baseName, string suffix1, string suffix2)
            : this(Extend(baseName.AsGiven, suffix1, suffix2, null), baseName.AsComponents.Append(suffix1).Append(suffix2))
        {
        }

        public CodeName(CodeName baseName, string suffix1, string suffix2, string suffix3)
            : this(Extend(baseName.AsGiven, suffix1, suffix2, suffix3), baseName.AsComponents.Append(suffix1).Append(suffix2).Append(suffix3))
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

        public string GetTypeName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, bool local = false) => language switch
        {
            TargetLanguage.CSharp => Escape(language, AsPascal(suffix1, suffix2, suffix3, suffix4)),
            TargetLanguage.Rust => Escape(language, AsPascal(suffix1, suffix2, suffix3, suffix4)),
            _ => AsPascal(suffix1, suffix2, suffix3, suffix4),
        };

        public string GetFieldName(TargetLanguage language) => language switch
        {
            TargetLanguage.CSharp => Escape(language, AsPascal()),
            TargetLanguage.Rust => Escape(language, AsSnake()),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetMethodName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null) => language switch
        {
            TargetLanguage.CSharp => Escape(language, AsPascal(suffix1, suffix2, suffix3, prefix: prefix)),
            TargetLanguage.Rust => Escape(language, AsSnake(suffix1, suffix2, suffix3, prefix: prefix)),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetVariableName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null) => language switch
        {
            TargetLanguage.CSharp => Escape(language, AsCamel(suffix1, suffix2, suffix3, prefix: prefix)),
            TargetLanguage.Rust => Escape(language, AsSnake(suffix1, suffix2, suffix3, prefix: prefix)),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetConstantName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? prefix = null) => language switch
        {
            TargetLanguage.CSharp => Escape(language, AsPascal(suffix1, suffix2, suffix3, prefix: prefix)),
            TargetLanguage.Rust => Escape(language, AsScreamingSnake(suffix1, suffix2, suffix3, prefix: prefix)),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetFileName(TargetLanguage language, string? suffix1 = null, string? suffix2 = null, string? suffix3 = null) => language switch
        {
            TargetLanguage.CSharp => AsPascal(suffix1, suffix2, suffix3),
            TargetLanguage.Rust => AsSnake(suffix1, suffix2, suffix3),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public string GetFolderName(TargetLanguage language) => language switch
        {
            TargetLanguage.CSharp => AsPascal(),
            TargetLanguage.Rust => AsSnake(),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };

        public bool IsEmpty => string.IsNullOrEmpty(givenName);

        public string AsGiven => givenName;

        private string[] AsComponents => components;

        private string AsLower(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, string? prefix = null)
        {
            return prefix ?? string.Empty + lowerName + suffix1 ?? string.Empty + suffix2 ?? string.Empty + suffix3 ?? string.Empty + suffix4 ?? string.Empty;
        }

        private string AsPascal(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, string? prefix = null)
        {
            return GetCapitalized(prefix) + pascalName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3) + GetCapitalized(suffix4);
        }

        private string AsCamel(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, string? prefix = null)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix + pascalName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3) + GetCapitalized(suffix4);
            }
            else if (!string.IsNullOrEmpty(givenName))
            {
                return camelName + GetCapitalized(suffix1) + GetCapitalized(suffix2) + GetCapitalized(suffix3) + GetCapitalized(suffix4);
            }
            else
            {
                return suffix1 + GetCapitalized(suffix2) + GetCapitalized(suffix3) + GetCapitalized(suffix4);
            }
        }

        private string AsSnake(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, string? prefix = null)
        {
            return GetSnakePrefix(prefix) + snakeName + GetSnakeSuffix(suffix1) + GetSnakeSuffix(suffix2) + GetSnakeSuffix(suffix3) + GetSnakeSuffix(suffix4);
        }

        private string AsScreamingSnake(string? suffix1 = null, string? suffix2 = null, string? suffix3 = null, string? suffix4 = null, string? prefix = null)
        {
            return AsSnake(suffix1, suffix2, suffix3, suffix4, prefix: prefix).ToUpperInvariant();
        }

        private static string Extend(string baseName, string suffix1, string? suffix2, string? suffix3)
        {
            bool snakeWise = baseName.Contains('_');
            StringBuilder givenName = new(baseName);
            givenName.Append(Extension(suffix1, snakeWise));

            if (suffix2 != null)
            {
                givenName.Append(Extension(suffix2, snakeWise));
            }

            if (suffix3 != null)
            {
                givenName.Append(Extension(suffix3, snakeWise));
            }

            return givenName.ToString();
        }

        private static List<string> DecomposeAndExtend(string baseName, string suffix1, string? suffix2, string? suffix3)
        {
            List<string> components = Decompose(baseName);
            components.Add(suffix1);

            if (suffix2 != null)
            {
                components.Add(suffix2);
            }

            if (suffix3 != null)
            {
                components.Add(suffix3);
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

        private static string Escape(TargetLanguage language, string name) => language switch
        {
            TargetLanguage.CSharp => ReservedCSharp.Keywords.Contains(name) ? $"@{name}" : name,
            TargetLanguage.Rust => ReservedRust.Keywords.Contains(name) ? $"r#{name}" : name,
            _ => name,
        };
    }
}
