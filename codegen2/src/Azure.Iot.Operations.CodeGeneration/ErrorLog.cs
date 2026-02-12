// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.TDParser.Model;

    public class ErrorLog
    {
        private readonly Dictionary<string, List<ValueReference>> referencesFromThings;
        private readonly Dictionary<(string, string), List<ValueReference>> typedReferencesFromThings;
        private readonly Dictionary<string, List<(string, int)>> namesOfThings;
        private readonly Dictionary<string, Dictionary<string, (string, int)>> namesInThings;
        private readonly Dictionary<string, List<ValueReference>> topicsInThings;
        private readonly Dictionary<string, List<(string, int)>> schemaNames;
        private readonly string defaultFolder;

        public string Phase { get; set; } = "Initialization";

        public HashSet<ErrorRecord> Warnings { get; init; }

        public HashSet<ErrorRecord> Errors { get; init; }

        public ErrorRecord? FatalError { get; private set; }

        public bool HasErrors => Errors.Count > 0 || FatalError != null;

        public ErrorLog(string defaultFolder)
        {
            this.referencesFromThings = new Dictionary<string, List<ValueReference>>();
            this.typedReferencesFromThings = new Dictionary<(string, string), List<ValueReference>>();
            this.namesOfThings = new Dictionary<string, List<(string, int)>>();
            this.namesInThings = new Dictionary<string, Dictionary<string, (string, int)>>();
            this.topicsInThings = new Dictionary<string, List<ValueReference>>();
            this.schemaNames = new Dictionary<string, List<(string, int)>>();
            this.defaultFolder = defaultFolder;

            Errors = new HashSet<ErrorRecord>();
            Warnings = new HashSet<ErrorRecord>();
            FatalError = null;
        }

        public void CheckForDuplicatesInThings()
        {
            foreach (var (name, nameSites) in namesOfThings)
            {
                if (nameSites.Count > 1)
                {
                    foreach (var (filename, lineNumber) in nameSites)
                    {
                        AddError(ErrorLevel.Error, ErrorCondition.Duplication, $"Duplicate use of Thing name '{name}'.", filename, lineNumber, crossRef: name);
                    }
                }
            }

            foreach (var (name, nameSites) in namesInThings)
            {
                if (nameSites.Count > 1)
                {
                    foreach (var (thingName, (filename, lineNumber)) in nameSites)
                    {
                        AddError(ErrorLevel.Error, ErrorCondition.Duplication, $"Duplicate use of generated name '{name}' across Thing Models.", filename, lineNumber, crossRef: name);
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

                        AddError(ErrorLevel.Error, ErrorCondition.Duplication, $"{description} '{topic}' used by multiple affordances{citation}.", reference.Filename, reference.LineNumber, crossRef: topic);
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
                        AddError(ErrorLevel.Error, ErrorCondition.Duplication, $"Duplicate use of generated name '{name}' across schema definitions.", filename, lineNumber, crossRef: name);
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

        public void RegisterNameOfThing(string name, string filename, int lineNumber)
        {
            if (!namesOfThings.TryGetValue(name, out List<(string, int)>? nameSites))
            {
                nameSites = new();
                namesOfThings[name] = nameSites;
            }
            nameSites.Add((filename, lineNumber));
        }

        public void RegisterNameInThing(string name, string thingName, string filename, int lineNumber)
        {
            if (!namesInThings.TryGetValue(name, out Dictionary<string, (string, int)>? nameSites))
            {
                nameSites = new();
                namesInThings[name] = nameSites;
            }

            if (!nameSites.ContainsKey(thingName))
            {
                nameSites[thingName] = (filename, lineNumber);
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
            if (!schemaNames.TryGetValue(name, out List<(string, int)>? nameSites))
            {
                nameSites = new();
                schemaNames[name] = nameSites;
            }

            if (dirpath.Equals(this.defaultFolder) && namesInThings.TryGetValue(name, out Dictionary<string, (string, int)>? thingNameSites))
            {
                foreach (KeyValuePair<string, (string, int)> thingNameSite in thingNameSites)
                {
                    nameSites.Add(thingNameSite.Value);
                }
            }
            else
            {
                nameSites.Add((filename, lineNumber));
            }
        }

        public void AddError(ErrorLevel level, ErrorCondition condition, string message, string filename, int lineNumber, int cfLineNumber = 0, string crossRef = "")
        {
            ErrorRecord errorRecord = new(condition, message, filename, lineNumber, cfLineNumber, crossRef);
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
                    AddError(ErrorLevel.Error, ErrorCondition.ItemNotFound, $"External schema reference \"{reference.Value}\" not resolvable; {reason}", reference.Filename, reference.LineNumber);
                }
            }
            else
            {
                AddError(ErrorLevel.Error, ErrorCondition.ItemNotFound, $"{description} \"{refValue}\" not resolvable; {reason}", filename, lineNumber);
            }
        }

        public void AddReferenceTypeError(string refPath, string description, string filename, string dirpath, int lineNumber, string refValue, string refType, string actualType)
        {
            var key = (refPath, refType);
            if (dirpath.Equals(this.defaultFolder) && typedReferencesFromThings.TryGetValue(key, out List<ValueReference>? typedReferences))
            {
                foreach (ValueReference reference in typedReferences)
                {
                    AddError(ErrorLevel.Error, ErrorCondition.TypeMismatch, $"External schema reference \"{reference.Value}\" is expected to define a schema of type \"{refType}\", but it defines a schema of type \"{actualType}\"", reference.Filename, reference.LineNumber);
                }
            }
            else
            {
                AddError(ErrorLevel.Error, ErrorCondition.TypeMismatch, $"{description} \"{refValue}\" is expected to define a schema of type \"{refType}\", but it defines a schema of type \"{actualType}\"", filename, lineNumber);
            }
        }
    }
}
