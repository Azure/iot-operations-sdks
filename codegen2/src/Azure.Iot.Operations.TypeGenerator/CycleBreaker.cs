// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    internal class CycleBreaker
    {
        private static readonly HashSet<TargetLanguage> LanguagesNeedingIndirection = new ()
        {
            TargetLanguage.Rust,
        };

        private bool indirectionNeeded;
        private readonly Dictionary<CodeName, List<CodeName>> directEdges;

        internal CycleBreaker(TargetLanguage targetLanguage)
        {
            this.indirectionNeeded = LanguagesNeedingIndirection.Contains(targetLanguage);
            this.directEdges = new Dictionary<CodeName, List<CodeName>>();
        }

        internal void AddIndirectionAsNeeded(SchemaType schemaType)
        {
            if (!this.indirectionNeeded)
            {
                return;
            }

            if (schemaType is ObjectType objectType)
            {
                List<CodeName> targets = new ();
                foreach (ObjectType.FieldInfo fieldInfo in objectType.FieldInfos.Values)
                {
                    if (!fieldInfo.IsIndirect && fieldInfo.SchemaType is ReferenceType referenceType)
                    {
                        if (referenceType.SchemaName.Equals(objectType.SchemaName) || CanReach(referenceType.SchemaName, objectType.SchemaName, new HashSet<CodeName>()))
                        {
                            fieldInfo.IsIndirect = true;
                        }
                        else
                        {
                            targets.Add(referenceType.SchemaName);
                        }
                    }
                }

                if (targets.Count > 0)
                {
                    this.directEdges[objectType.SchemaName] = targets;
                }
            }
        }

        private bool CanReach(CodeName source, CodeName endpoint, HashSet<CodeName> visited)
        {
            if (this.directEdges.TryGetValue(source, out List<CodeName>? targets) && visited.Add(source))
            {
                foreach (CodeName target in targets)
                {
                    if (target.Equals(endpoint) || CanReach(target, endpoint, visited))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
