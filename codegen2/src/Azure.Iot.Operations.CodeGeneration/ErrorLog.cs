namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.IO;

    public class ErrorLog
    {
        private readonly Dictionary<string, Dictionary<string, int>> registeredNames;
        private readonly Dictionary<string, List<ExternalReference>> registeredReferences;
        private readonly string defaultFolder;

        public HashSet<ErrorRecord> Warnings { get; init; }

        public HashSet<ErrorRecord> Errors { get; init; }

        public ErrorRecord? FatalError { get; private set; }

        public bool HasErrors => Errors.Count > 0 || FatalError != null;

        public ErrorLog(string defaultFolder)
        {
            this.registeredNames = new Dictionary<string, Dictionary<string, int>>();
            this.registeredReferences = new Dictionary<string, List<ExternalReference>>();
            this.defaultFolder = defaultFolder;

            Errors = new HashSet<ErrorRecord>();
            Warnings = new HashSet<ErrorRecord>();
            FatalError = null;
        }

        public void CheckForDuplicates()
        {
            foreach (var (name, registrations) in registeredNames)
            {
                if (registrations.Count > 1)
                {
                    foreach (var (filename, lineNumber) in registrations)
                    {
                        AddError(ErrorLevel.Error, $"Duplicate use of generated name '{name}' across Thing Descriptions.", filename, lineNumber, crossRef: name);
                    }
                }
            }
        }

        public void RegisterName(string name, string filename, int lineNumber)
        {
            if (!registeredNames.TryGetValue(name, out Dictionary<string, int>? registrations))
            {
                registrations = new();
                registeredNames[name] = registrations;
            }

            if (!registrations.TryGetValue(filename, out int extantLineNumber) || extantLineNumber < 0)
            {
                registrations[filename] = lineNumber;
            }
        }

        public void RegisterReference(string refPath, string filename, int lineNumber, string refValue)
        {
            string fullPath = Path.GetFullPath(Path.Combine(this.defaultFolder, refValue)).Replace('\\', '/');
            if (!registeredReferences.TryGetValue(refPath, out List<ExternalReference>? references))
            {
                references = new();
                registeredReferences[refPath] = references;
            }

            references.Add(new ExternalReference(filename, lineNumber, refValue));
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
            if (registeredReferences.TryGetValue(refPath, out List<ExternalReference>? references))
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
