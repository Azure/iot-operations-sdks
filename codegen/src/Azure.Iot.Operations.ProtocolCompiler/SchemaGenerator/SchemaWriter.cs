namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class SchemaWriter
    {
        private readonly string parentPath;

        private Dictionary<string, int> schemaCounts;

        public SchemaWriter(string parentPath, Dictionary<string, int> schemaCounts)
        {
            this.parentPath = parentPath;
            this.schemaCounts = schemaCounts;
        }

        public void Accept(string schemaText, string fileName, string subFolder)
        {
            string folderPath = Path.Combine(parentPath, subFolder);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);
            string schemaName = fileName.Substring(0, fileName.IndexOf('.'));

            if (schemaCounts.TryGetValue(schemaName, out int count))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  COLLISION {filePath}");
                Console.ResetColor();
                schemaCounts[schemaName]++;
            }
            else
            {
                File.WriteAllText(filePath, schemaText);
                Console.WriteLine($"  generated {filePath}");
                schemaCounts[schemaName] = 1;
            }
        }
    }
}
