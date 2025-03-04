// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Support
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// An implementation of <c>IReadOnlyDictionary</c> that combines two <c>IReadOnlyDictionary</c> objects by prefixng their string keys.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the combined dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the combined dictionary.</typeparam>
    public class CombinedPrefixedReadOnlyDictionary<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly string prefix1;
        private readonly IReadOnlyDictionary<string, TValue> dict1;
        private readonly string prefix2;
        private readonly IReadOnlyDictionary<string, TValue> dict2;

        /// <summary>
        /// Initializes a new instance of the <see cref="CombinedPrefixedReadOnlyDictionary{TValue}"/> class.
        /// </summary>
        /// <param name="prefix1">The prefix for keys in <paramref name="dict1"/>.</param>
        /// <param name="dict1">One of the <c>IReadOnlyDictionary</c> objects to combine.</param>
        /// <param name="prefix2">The prefix for keys in <paramref name="dict2"/>.</param>
        /// <param name="dict2">The other <c>IReadOnlyDictionary</c> object to combine.</param>
        public CombinedPrefixedReadOnlyDictionary(
            string prefix1,
            IReadOnlyDictionary<string, TValue> dict1,
            string prefix2,
            IReadOnlyDictionary<string, TValue> dict2)
        {
            ArgumentNullException.ThrowIfNull(prefix1, nameof(prefix1));
            ArgumentNullException.ThrowIfNull(dict1, nameof(dict1));
            ArgumentNullException.ThrowIfNull(prefix2, nameof(prefix2));
            ArgumentNullException.ThrowIfNull(dict2, nameof(dict2));

            this.prefix1 = prefix1;
            this.dict1 = dict1;
            this.prefix2 = prefix2;
            this.dict2 = dict2;
        }

        /// <inheritdoc/>
        IEnumerable<string> IReadOnlyDictionary<string, TValue>.Keys => dict1.Keys.Select(k => $"{prefix1}{k}").Concat(dict2.Keys.Select(k => $"{prefix2}{k}"));

        /// <inheritdoc/>
        IEnumerable<TValue> IReadOnlyDictionary<string, TValue>.Values => dict1.Values.Concat(dict2.Values);

        /// <inheritdoc/>
        int IReadOnlyCollection<KeyValuePair<string, TValue>>.Count => dict1.Count + dict2.Count;

        /// <inheritdoc/>
        TValue IReadOnlyDictionary<string, TValue>.this[string key] =>
            key.StartsWith(prefix1, StringComparison.InvariantCulture) && dict1.TryGetValue(key.Substring(prefix1.Length), out TValue? value1) ? value1 :
            key.StartsWith(prefix2, StringComparison.InvariantCulture) && dict2.TryGetValue(key.Substring(prefix2.Length), out TValue? value2) ? value2 :
            default(TValue)!;

        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, TValue>.ContainsKey(string key)
        {
            return
                key.StartsWith(prefix1, StringComparison.InvariantCulture) && dict1.ContainsKey(key.Substring(prefix1.Length)) ||
                key.StartsWith(prefix2, StringComparison.InvariantCulture) && dict2.ContainsKey(key.Substring(prefix2.Length));
        }

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<string, TValue>> IEnumerable<KeyValuePair<string, TValue>>.GetEnumerator()
        {
            foreach (var item in dict1)
            {
                yield return new KeyValuePair<string, TValue>($"{prefix1}{item.Key}", item.Value);
            }

            foreach (var item in dict2)
            {
                yield return new KeyValuePair<string, TValue>($"{prefix2}{item.Key}", item.Value);
            }
        }

        /// <inheritdoc/>
        bool IReadOnlyDictionary<string, TValue>.TryGetValue(string key, out TValue value)
        {
            if (key.StartsWith(prefix1, StringComparison.InvariantCulture) && dict1.TryGetValue(key.Substring(prefix1.Length), out TValue? value1))
            {
                value = value1;
                return true;
            }

            if (key.StartsWith(prefix2, StringComparison.InvariantCulture) && dict2.TryGetValue(key.Substring(prefix2.Length), out TValue? value2))
            {
                value = value2;
                return true;
            }

            value = default(TValue)!;
            return false;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, TValue>>)this).GetEnumerator();
        }
    }
}
