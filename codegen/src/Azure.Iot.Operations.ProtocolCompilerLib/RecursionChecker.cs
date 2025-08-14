namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public class RecursionChecker
    {
        private readonly Dictionary<CodeName, List<CodeName>> directEdges;

        public RecursionChecker()
        {
            this.directEdges = new Dictionary<CodeName, List<CodeName>>();
        }

        public bool TryDetectLoop(SchemaType schemaType, [NotNullWhen(true)] out CodeName? recursiveSchemaName)
        {
            if (schemaType is ObjectType objectType)
            {
                List<CodeName> targets = new ();
                foreach (ObjectType.FieldInfo fieldInfo in objectType.FieldInfos.Values)
                {
                    if (!fieldInfo.IsIndirect && fieldInfo.SchemaType is ReferenceType referenceType)
                    {
                        if (referenceType.SchemaName.Equals(objectType.SchemaName) || CanReach(referenceType.SchemaName, objectType.SchemaName))
                        {
                            recursiveSchemaName = objectType.SchemaName;
                            return true;
                        }

                        targets.Add(referenceType.SchemaName);
                    }
                }

                if (targets.Count > 0)
                {
                    this.directEdges[objectType.SchemaName] = targets;
                }
            }

            recursiveSchemaName = null;
            return false;
        }

        private bool CanReach(CodeName source, CodeName endpoint)
        {
            if (this.directEdges.TryGetValue(source, out List<CodeName>? targets))
            {
                foreach (CodeName target in targets)
                {
                    if (target.Equals(endpoint) || CanReach(target, endpoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
