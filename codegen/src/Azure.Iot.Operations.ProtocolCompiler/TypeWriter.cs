namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;

    public class TypeWriter
    {
        private readonly string parentPath;

        public TypeWriter(string parentPath)
        {
            this.parentPath = parentPath;
        }

        public void Accept(string typeText, string fileName, string subFolder)
        {
            string folderPath = Path.Combine(parentPath, subFolder);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);

            File.WriteAllText(filePath, typeText);
            Console.WriteLine($"  generated {filePath}");
        }
    }
}
