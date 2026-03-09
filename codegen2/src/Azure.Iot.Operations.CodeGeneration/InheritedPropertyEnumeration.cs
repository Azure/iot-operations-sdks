// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections;
    using System.Collections.Generic;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public class InheritedPropertyEnumeration : IEnumerable<KeyValuePair<string, ValueTracker<TDProperty>>>
    {
        private IResolvingThing resolvingThing;
        private InheritanceEnumeration inheritanceEnumeration;

        public InheritedPropertyEnumeration(IResolvingThing resolvingThing)
        {
            this.resolvingThing = resolvingThing;
            inheritanceEnumeration = new InheritanceEnumeration(resolvingThing);
        }

        public bool DoesInherit { get => inheritanceEnumeration.DoesInherit; }

        public IEnumerator<KeyValuePair<string, ValueTracker<TDProperty>>> GetEnumerator()
        {
            if (resolvingThing.ParsedThing.Thing.Properties?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDProperty>> kvp in resolvingThing.ParsedThing.Thing.Properties.Entries!)
                {
                    yield return kvp;
                }
            }

            foreach (InheritanceEnumerator inheritanceEnumerator in inheritanceEnumeration)
            {
                foreach (KeyValuePair<string, ValueTracker<TDProperty>> kvp in inheritanceEnumerator.EnumerateProperties())
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
