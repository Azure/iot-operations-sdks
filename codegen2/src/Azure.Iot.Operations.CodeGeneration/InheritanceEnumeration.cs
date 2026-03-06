// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public class InheritanceEnumeration : IEnumerable<InheritanceEnumerator>
    {
        private IResolvingThing resolvingThing;

        public InheritanceEnumeration(IResolvingThing resolvingThing)
        {
            this.resolvingThing = resolvingThing;
            DoesInherit = resolvingThing.ParsedThing.Thing.Links?.Elements?.Any(l => l.Value.Rel?.Value.Value == TDValues.RelationExtends) ?? false;
        }

        public bool DoesInherit { get; }

        public IEnumerator<InheritanceEnumerator> GetEnumerator()
        {
            IResolvingThing currentThing = resolvingThing;

            HashSet<string> seenActions = new HashSet<string>();
            HashSet<string> seenEvents = new HashSet<string>();
            HashSet<string> seenProps = new HashSet<string>();

            while (true)
            {
                ValueTracker<TDLink>? extendsLink = currentThing.ParsedThing.Thing.Links?.Elements?.FirstOrDefault(l => l.Value.Rel?.Value.Value == TDValues.RelationExtends);

                if (extendsLink?.Value.Href == null || !currentThing.TryResolve(extendsLink.Value.Href.Value.Value, out IResolvingThing? extendedThing))
                {
                    yield break;
                }

                seenActions.UnionWith(currentThing.ParsedThing.Thing.Actions?.Entries?.Select(a => a.Key) ?? Enumerable.Empty<string>());
                seenEvents.UnionWith(currentThing.ParsedThing.Thing.Events?.Entries?.Select(e => e.Key) ?? Enumerable.Empty<string>());
                seenProps.UnionWith(currentThing.ParsedThing.Thing.Properties?.Entries?.Select(p => p.Key) ?? Enumerable.Empty<string>());

                yield return new InheritanceEnumerator(extendedThing, seenActions, seenEvents, seenProps);

                currentThing = extendedThing;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
