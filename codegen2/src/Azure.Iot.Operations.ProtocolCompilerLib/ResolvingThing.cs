// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    public class ResolvingThing : IResolvingThing
    {
        private Dictionary<string, Dictionary<string, ParsedThing>> filepathToTitleToParsedThingMap;

        public ResolvingThing(ParsedThing parsedThing, Dictionary<string, Dictionary<string, ParsedThing>> filepathToTitleToParsedThingMap)
        {
            this.filepathToTitleToParsedThingMap = filepathToTitleToParsedThingMap;
            ParsedThing = parsedThing;
        }

        public ParsedThing ParsedThing { get; }

        public bool TryResolve(string href, [NotNullWhen(true)] out IResolvingThing? resolvingThing)
        {
            int hashIx = href.IndexOf('#');

            string baseName = hashIx > 0 ? href.Substring(0, hashIx) : ParsedThing.FileName;
            string filepath = Path.GetFullPath(Path.Combine(ParsedThing.DirectoryName, baseName)).Replace('\\', '/');

            string fragment = href.Substring(hashIx + 1);
            string title = fragment.StartsWith(TDValues.HrefTitlePrefix) ? fragment.Substring(TDValues.HrefTitlePrefix.Length) : string.Empty;

            if (filepathToTitleToParsedThingMap.TryGetValue(filepath, out Dictionary<string, ParsedThing>? titleToParsedThingMap) &&
                titleToParsedThingMap.TryGetValue(title, out ParsedThing? referencedThing))
            {
                resolvingThing = new ResolvingThing(referencedThing, filepathToTitleToParsedThingMap);
                return true;
            }

            resolvingThing = null;
            return false;
        }
    }
}
