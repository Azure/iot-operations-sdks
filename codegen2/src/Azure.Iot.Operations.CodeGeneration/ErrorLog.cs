namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.TDParser.Model;

    public class ErrorLog
    {
        private readonly Dictionary<string, List<KeyValuePair<string, int>>> idsOfThings;
        private readonly Dictionary<string, List<ValueReference>> referencesFromThings;
        private readonly Dictionary<(string, string), List<ValueReference>> typedReferencesFromThings;
        private readonly Dictionary<string, Dictionary<string, int>> namesInThings;
        private readonly Dictionary<string, List<ValueReference>> topicsInThings;
        private readonly Dictionary<string, List<KeyValuePair<string, int>>> schemaNames;
        private readonly string defaultFolder;

        public string Phase { get; set; } = "Initialization";

        public HashSet<ErrorRecord> Warnings { get; init; }

        public HashSet<ErrorRecord> Errors { get; init; }

        public ErrorRecord? FatalError { get; private set; }

        public bool HasErrors => Errors.Count > 0 || FatalError != null;

        public ErrorLog(string defaultFolder)
        {
            this.idsOfThings = new Dictionary<string, List<KeyValuePair<string, int>>>();
            this.referencesFromThings = new Dictionary<string, List<ValueReference>>();
            this.typedReferencesFromThings = new Dictionary<(string, string), List<ValueReference>>();
            this.namesInThings = new Dictionary<string, Dictionary<string, int>>();
            this.topicsInThings = new Dictionary<string, List<ValueReference>>();
            this.schemaNames = new Dictionary<string, List<KeyValuePair<string, int>>>();
            this.defaultFolder = defaultFolder;

            Errors = new HashSet<ErrorRecord>();
            Warnings = new HashSet<ErrorRecord>();
            FatalError = null;
        }

        public void CheckForDuplicatesInThings()
        {
            foreach (var (id, idSites) in idsOfThings)
            {
                if (idSites.Count > 1)
                {
                    foreach (var (filename, lineNumber) in idSites)
                    {
                        AddError(ErrorLevel.Error, $"Duplicate use of '{TDThing.IdName}' value '{id}' across TDs.", filename, lineNumber, crossRef: id);
                    }
                }
            }

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

            foreach (var (topic, topicReferences) in topicsInThings)
            {
                if (topicReferences.Count > 1)
                {
                    foreach (ValueReference reference in topicReferences)
                    {
                        string description;
                        string citation;
                        if (topic == reference.Value)
                        {
                            description = "Topic";
                            citation = string.Empty;
                        }
                        else
                        {
                            description = topic.Contains('{') ? "Partially resolved topic" : "Resolved topic";
                            citation = $" (in model as \"{reference.Value}\")";
                        }

                        AddError(ErrorLevel.Error, $"{description} '{topic}' used by multiple affordances{citation}.", reference.Filename, reference.LineNumber, crossRef: topic);
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
            string fullPath = Path.GetFullPath(Path.Combine(this.defaultFolder, refPath)).Replace('\\', '/');

            if (!referencesFromThings.TryGetValue(fullPath, out List<ValueReference>? references))
            {
                references = new();
                referencesFromThings[fullPath] = references;
            }
            references.Add(new ValueReference(filename, lineNumber, refValue));
        }

        public void RegisterTypedReferenceFromThing(string refPath, string filename, int lineNumber, string type, string refValue)
        {
            string fullPath = Path.GetFullPath(Path.Combine(this.defaultFolder, refPath)).Replace('\\', '/');
            var key = (fullPath, type);

            if (!typedReferencesFromThings.TryGetValue(key, out List<ValueReference>? typedReferences))
            {
                typedReferences = new();
                typedReferencesFromThings[key] = typedReferences;
            }
            typedReferences.Add(new ValueReference(filename, lineNumber, refValue));
        }

        public void RegisterIdOfThing(string id, string filename, int lineNumber)
        {
            if (!idsOfThings.TryGetValue(id, out List<KeyValuePair<string, int>>? idSites))
            {
                idSites = new();
                idsOfThings[id] = idSites;
            }

            idSites.Add(new KeyValuePair<string, int>(filename, lineNumber));
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

        public void RegisterTopicInThing(string resolvedTopic, string filename, int lineNumber, string rawTopic)
        {
            if (!topicsInThings.TryGetValue(resolvedTopic, out List<ValueReference>? topicReferences))
            {
                topicReferences = new();
                topicsInThings[resolvedTopic] = topicReferences;
            }
            topicReferences.Add(new ValueReference(filename, lineNumber, rawTopic));
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

        public void AddError(ErrorLevel level, string message, string filename, int lineNumber, int cfLineNumber = 0, string crossRef = "")
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

        public void AddReferenceError(string refPath, string description, string reason, string filename, string dirpath, int lineNumber, string refValue)
        {
            if (dirpath.Equals(this.defaultFolder) && referencesFromThings.TryGetValue(refPath, out List<ValueReference>? references))
            {
                foreach (ValueReference reference in references)
                {
                    AddError(ErrorLevel.Error, $"External schema reference \"{reference.Value}\" not resolvable; {reason}", reference.Filename, reference.LineNumber);
                }
            }
            else
            {
                AddError(ErrorLevel.Error, $"{description} \"{refValue}\" not resolvable; {reason}", filename, lineNumber);
            }
        }

        public void AddReferenceTypeError(string refPath, string description, string filename, string dirpath, int lineNumber, string refValue, string refType, string actualType)
        {
            var key = (refPath, refType);
            if (dirpath.Equals(this.defaultFolder) && typedReferencesFromThings.TryGetValue(key, out List<ValueReference>? typedReferences))
            {
                foreach (ValueReference reference in typedReferences)
                {
                    AddError(ErrorLevel.Error, $"External schema reference \"{reference.Value}\" is expected to define a schema of type \"{refType}\", but it defines a schema of type \"{actualType}\"", reference.Filename, reference.LineNumber);
                }
            }
            else
            {
                AddError(ErrorLevel.Error, $"{description} \"{refValue}\" is expected to define a schema of type \"{refType}\", but it defines a schema of type \"{actualType}\"", filename, lineNumber);
            }
        }
    }
}
