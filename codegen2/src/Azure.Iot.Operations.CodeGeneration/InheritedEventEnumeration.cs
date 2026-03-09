// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections;
    using System.Collections.Generic;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public class InheritedEventEnumeration : IEnumerable<KeyValuePair<string, ValueTracker<TDEvent>>>
    {
        private IResolvingThing resolvingThing;
        private InheritanceEnumeration inheritanceEnumeration;

        public InheritedEventEnumeration(IResolvingThing resolvingThing)
        {
            this.resolvingThing = resolvingThing;
            inheritanceEnumeration = new InheritanceEnumeration(resolvingThing);
        }

        public bool DoesInherit { get => inheritanceEnumeration.DoesInherit; }

        public IEnumerator<KeyValuePair<string, ValueTracker<TDEvent>>> GetEnumerator()
        {
            if (resolvingThing.ParsedThing.Thing.Events?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDEvent>> kvp in resolvingThing.ParsedThing.Thing.Events.Entries!)
                {
                    yield return kvp;
                }
            }

            foreach (InheritanceEnumerator inheritanceEnumerator in inheritanceEnumeration)
            {
                foreach (KeyValuePair<string, ValueTracker<TDEvent>> kvp in inheritanceEnumerator.EnumerateEvents())
                {
                    yield return kvp;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
