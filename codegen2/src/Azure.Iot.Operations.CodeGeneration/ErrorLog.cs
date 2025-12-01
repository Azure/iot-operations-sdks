namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.IO;

    public class ErrorLog
    {
        private readonly Dictionary<string, List<ExternalReference>> referencesFromThings;
        private readonly Dictionary<string, Dictionary<string, int>> namesInThings;
        private readonly Dictionary<string, List<KeyValuePair<string, int>>> schemaNames;
        private readonly string defaultFolder;

        public HashSet<ErrorRecord> Warnings { get; init; }

        public HashSet<ErrorRecord> Errors { get; init; }

        public ErrorRecord? FatalError { get; private set; }

        public bool HasErrors => Errors.Count > 0 || FatalError != null;

        public ErrorLog(string defaultFolder)
        {
            this.referencesFromThings = new Dictionary<string, List<ExternalReference>>();
            this.namesInThings = new Dictionary<string, Dictionary<string, int>>();
            this.schemaNames = new Dictionary<string, List<KeyValuePair<string, int>>>();
            this.defaultFolder = defaultFolder;

            Errors = new HashSet<ErrorRecord>();
            Warnings = new HashSet<ErrorRecord>();
            FatalError = null;
        }

        public void CheckForDuplicatesInThings()
        {
            foreach (var (name, nameSites) in namesInThings)
            {
                if (nameSites.Count > 1)
                {
                    foreach (var (filename, lineNumber) in nameSites)
                    {
                        AddError(ErrorLevel.Error, $"Duplicate use of generated name '{name}' across Thing Descriptions.", filename, lineNumber, crossRef: name);
                    }
                }
            }
        }

        public void CheckForDuplicatesInSchemas()
        {
            foreach (var (name, nameSites) in schemaNames)
            {
                if (nameSites.Count > 1)
                {
                    foreach (var (filename, lineNumber) in nameSites)
                    {
                        AddError(ErrorLevel.Error, $"Duplicate use of generated name '{name}' across schema definitions.", filename, lineNumber, crossRef: name);
                    }
                }
            }
        }

        public void RegisterReferenceFromThing(string refPath, string filename, int lineNumber, string refValue)
        {
            string fullPath = Path.GetFullPath(Path.Combine(this.defaultFolder, refValue)).Replace('\\', '/');
            if (!referencesFromThings.TryGetValue(refPath, out List<ExternalReference>? references))
            {
                references = new();
                referencesFromThings[refPath] = references;
            }

            references.Add(new ExternalReference(filename, lineNumber, refValue));
        }

        public void RegisterNameInThing(string name, string filename, int lineNumber)
        {
            if (!namesInThings.TryGetValue(name, out Dictionary<string, int>? nameSites))
            {
                nameSites = new();
                namesInThings[name] = nameSites;
            }

            if (!nameSites.TryGetValue(filename, out int extantLineNumber) || extantLineNumber < 0)
            {
                nameSites[filename] = lineNumber;
            }
        }

        public void RegisterSchemaName(string name, string filename, string dirpath, int lineNumber)
        {
            if (!schemaNames.TryGetValue(name, out List<KeyValuePair<string, int>>? nameSites))
            {
                nameSites = new();
                schemaNames[name] = nameSites;
            }

            if (dirpath.Equals(this.defaultFolder) && namesInThings.TryGetValue(name, out Dictionary<string, int>? thingNameSites))
            {
                foreach (KeyValuePair<string, int> thingNameSite in thingNameSites)
                {
                    nameSites.Add(thingNameSite);
                }
            }
            else
            {
                nameSites.Add(new KeyValuePair<string, int>(filename, lineNumber));
            }
        }

        public void AddError(ErrorLevel level, string message, string filename, int lineNumber, int cfLineNumber = -1, string crossRef = "")
        {
            ErrorRecord errorRecord = new(message, filename, lineNumber, cfLineNumber, crossRef);
            switch (level)
            {
                case ErrorLevel.Warning:
                    this.Warnings.Add(errorRecord);
                    break;
                case ErrorLevel.Error:
                    this.Errors.Add(errorRecord);
                    break;
                case ErrorLevel.Fatal:
                    FatalError = errorRecord;
                    break;
            }
        }

        public void AddReferenceError(string refPath, string description, string reason, string filename, int lineNumber, string refValue)
        {
            if (referencesFromThings.TryGetValue(refPath, out List<ExternalReference>? references))
            {
                foreach (ExternalReference reference in references)
                {
                    AddError(ErrorLevel.Error, $"External schema reference \"{reference.RefValue}\" not resolvable; {reason}", reference.Filename, reference.LineNumber);
                }
            }
            else
            {
                AddError(ErrorLevel.Error, $"{description} \"{refValue}\" not resolvable; {reason}", filename, lineNumber);
            }
        }
    }
}
