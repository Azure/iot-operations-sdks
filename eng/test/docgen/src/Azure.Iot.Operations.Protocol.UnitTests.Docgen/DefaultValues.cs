namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Tomlyn;
    using Tomlyn.Model;

    public class DefaultValues
    {
        private const string defaultsFileName = "defaults.toml";

        private Dictionary<string, TomlTable> defaultTables;

        public DefaultValues(string testCaseRoot)
        {
            defaultTables = new Dictionary<string, TomlTable>();
            foreach (string dirPath in Directory.GetDirectories(testCaseRoot))
            {
                string suiteName = Path.GetFileName(dirPath);
                string filePath = Path.Combine(dirPath, defaultsFileName);
                defaultTables[suiteName] = Toml.ToModel(File.ReadAllText(filePath), filePath);
            }
        }

        public string? GetDefaultAsString(string suiteName, string tablePath, string propertyName, string[] popDefaults)
        {
            if (popDefaults.Contains(propertyName))
            {
                return "(see below)";
            }

            string[] tablePathSegments = tablePath.Split('.');
            if (suiteName != string.Empty)
            {
                return GetPathPropertyValue(defaultTables[suiteName], tablePathSegments, propertyName);
            }

            IEnumerable<string?> defaults = defaultTables.Select(kvp => GetPathPropertyValue(kvp.Value, tablePathSegments, propertyName));
            string? commonDefault = defaults.First();
            if (defaults.Any(d => d != commonDefault))
            {
                Alert.Error($"No common default value across all suites for property {propertyName}");
                return "**ERROR**";
            }

            return commonDefault;
        }

        private string? GetPathPropertyValue(TomlTable table, IEnumerable<string> path, string propertyName)
        {
            if (path.Any())
            {
                return table.TryGetValue(path.First(), out object? nextTable) ? GetPathPropertyValue((TomlTable)nextTable, path.Skip(1), propertyName) : null;
            }

            if (!table.TryGetValue(propertyName, out object? value))
            {
                return null;
            }

            return GetValue(value);
        }

        private string GetValue(object value)
        {
            if (value is TomlTable tomlTable)
            {
                return "{ " + string.Join(", ", tomlTable.Select(kvp => $"{GetValue(kvp.Key)}: {GetValue(kvp.Value)}")) + " }";
            }

            if (value is TomlArray tomlArray)
            {
                return "[ " + string.Join(", ", tomlArray.Select(elt => GetValue(elt!))) + " ]";
            }

            return value is bool boolVal? (boolVal ? "true" : "false") : value is string strVal ? $"\"{strVal}\"" : value.ToString()!;
        }
    }
}
