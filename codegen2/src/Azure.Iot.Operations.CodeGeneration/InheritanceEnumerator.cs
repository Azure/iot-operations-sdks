// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public class InheritanceEnumerator
    {
        private IResolvingThing resolvingThing;
        private HashSet<string> seenActions;
        private HashSet<string> seenEvents;
        private HashSet<string> seenProps;

        public InheritanceEnumerator(IResolvingThing resolvingThing, HashSet<string> seenActions, HashSet<string> seenEvents, HashSet<string> seenProps)
        {
            this.resolvingThing = resolvingThing;
            this.seenActions = seenActions;
            this.seenEvents = seenEvents;
            this.seenProps = seenProps;
        }

        public ParsedThing ParsedThing { get => resolvingThing.ParsedThing; }

        public IEnumerable<KeyValuePair<string, ValueTracker<TDAction>>> EnumerateActions()
        {
            if (resolvingThing.ParsedThing.Thing.Actions?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDAction>> kvp in resolvingThing.ParsedThing.Thing.Actions.Entries!)
                {
                    if (!seenActions.Contains(kvp.Key))
                    {
                        yield return kvp;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<string, ValueTracker<TDEvent>>> EnumerateEvents()
        {
            if (resolvingThing.ParsedThing.Thing.Events?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDEvent>> kvp in resolvingThing.ParsedThing.Thing.Events.Entries!)
                {
                    if (!seenEvents.Contains(kvp.Key))
                    {
                        yield return kvp;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<string, ValueTracker<TDProperty>>> EnumerateProperties()
        {
            if (resolvingThing.ParsedThing.Thing.Properties?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDProperty>> kvp in resolvingThing.ParsedThing.Thing.Properties.Entries!)
                {
                    if (!seenProps.Contains(kvp.Key))
                    {
                        yield return kvp;
                    }
                }
            }
        }
    }
}
