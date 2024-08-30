namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    public class JsonSchemata : IDisposable
    {
        private const string schemaExtension = ".schema.json";

        private Dictionary<string, JsonDocument> schemata;

        public JsonSchemata(string folderPath)
        {
            schemata = new Dictionary<string, JsonDocument>();
            foreach (string filePath in Directory.GetFiles(folderPath, $"*{schemaExtension}"))
            {
                string fileName = Path.GetFileName(filePath);
                string schemaName = fileName.Substring(0, fileName.Length - schemaExtension.Length);
                schemata[schemaName] = JsonDocument.Parse(File.ReadAllText(filePath));
            }
        }

        public IEnumerable<string> GetSchemaNames()
        {
            return schemata.Keys;
        }

        public JsonDocument GetSchema(string name)
        {
            return schemata[name];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (KeyValuePair<string, JsonDocument> kvp in schemata)
                {
                    kvp.Value.Dispose();
                }
            }
        }
    }
}
