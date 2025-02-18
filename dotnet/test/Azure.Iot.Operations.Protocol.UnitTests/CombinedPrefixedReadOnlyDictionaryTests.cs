// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Support;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class CombinedPrefixedReadOnlyDictionaryTests
    {
        private const string prefix1 = "foo:";
        private const string prefix2 = "bar:";

        private Dictionary<string, int> _dict1;
        private Dictionary<string, int> _dict2;
        private IReadOnlyDictionary<string, int> _combinedDict;

        public CombinedPrefixedReadOnlyDictionaryTests()
        {
            _dict1 = new()
            {
                { "alpha", 1 },
                { "beta", 3 },
                { "gamma", 5 },
                { "delta", 7 },
                { "epsilon", 9 },
            };

            _dict2 = new()
            {
                { "alpha", 1 },
                { "bravo", 1 },
                { "charlie", 2 },
                { "delta", 3 },
                { "echo", 5 },
                { "foxtrot", 8 },
            };

            _combinedDict = new CombinedPrefixedReadOnlyDictionary<int>(prefix1, _dict1, prefix2, _dict2);
        }

        [Fact]
        public void KeysEnumerationIncludesAll()
        {
            Assert.Contains($"{prefix1}alpha", _combinedDict.Keys);
            Assert.Contains($"{prefix1}beta", _combinedDict.Keys);
            Assert.Contains($"{prefix1}gamma", _combinedDict.Keys);
            Assert.Contains($"{prefix1}delta", _combinedDict.Keys);
            Assert.Contains($"{prefix1}epsilon", _combinedDict.Keys);

            Assert.Contains($"{prefix2}alpha", _combinedDict.Keys);
            Assert.Contains($"{prefix2}bravo", _combinedDict.Keys);
            Assert.Contains($"{prefix2}charlie", _combinedDict.Keys);
            Assert.Contains($"{prefix2}delta", _combinedDict.Keys);
            Assert.Contains($"{prefix2}echo", _combinedDict.Keys);
            Assert.Contains($"{prefix2}foxtrot", _combinedDict.Keys);
        }

        [Fact]
        public void KeysEnumerationNotPolluted()
        {
            Assert.DoesNotContain($"{prefix2}beta", _combinedDict.Keys);
            Assert.DoesNotContain($"{prefix2}gamma", _combinedDict.Keys);
            Assert.DoesNotContain($"{prefix2}epsilon", _combinedDict.Keys);

            Assert.DoesNotContain($"{prefix1}bravo", _combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}charlie", _combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}echo", _combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}foxtrot", _combinedDict.Keys);
        }

        [Fact]
        public void ValuesEnumerationIncludesUnion()
        {
            Assert.Contains(1, _combinedDict.Values);
            Assert.Contains(2, _combinedDict.Values);
            Assert.Contains(3, _combinedDict.Values);
            Assert.Contains(5, _combinedDict.Values);
            Assert.Contains(7, _combinedDict.Values);
            Assert.Contains(8, _combinedDict.Values);
            Assert.Contains(9, _combinedDict.Values);
        }

        [Fact]
        public void CountMatchesTotal()
        {
            Assert.Equal(_dict1.Count + _dict2.Count, ((IReadOnlyDictionary<string, int>)_combinedDict).Count);
        }

        [Fact]
        public void LookupFindsAppropriateValue()
        {
            Assert.Equal(1, _combinedDict[$"{prefix1}alpha"]);
            Assert.Equal(3, _combinedDict[$"{prefix1}beta"]);
            Assert.Equal(5, _combinedDict[$"{prefix1}gamma"]);
            Assert.Equal(7, _combinedDict[$"{prefix1}delta"]);
            Assert.Equal(9, _combinedDict[$"{prefix1}epsilon"]);

            Assert.Equal(1, _combinedDict[$"{prefix2}alpha"]);
            Assert.Equal(1, _combinedDict[$"{prefix2}bravo"]);
            Assert.Equal(2, _combinedDict[$"{prefix2}charlie"]);
            Assert.Equal(3, _combinedDict[$"{prefix2}delta"]);
            Assert.Equal(5, _combinedDict[$"{prefix2}echo"]);
            Assert.Equal(8, _combinedDict[$"{prefix2}foxtrot"]);
        }

        [Fact]
        public void ContainsKeyReturnsTrueWhenPresent()
        {
            Assert.True(_combinedDict.ContainsKey($"{prefix1}alpha"));
            Assert.True(_combinedDict.ContainsKey($"{prefix1}beta"));
            Assert.True(_combinedDict.ContainsKey($"{prefix1}gamma"));
            Assert.True(_combinedDict.ContainsKey($"{prefix1}delta"));
            Assert.True(_combinedDict.ContainsKey($"{prefix1}epsilon"));

            Assert.True(_combinedDict.ContainsKey($"{prefix2}alpha"));
            Assert.True(_combinedDict.ContainsKey($"{prefix2}bravo"));
            Assert.True(_combinedDict.ContainsKey($"{prefix2}charlie"));
            Assert.True(_combinedDict.ContainsKey($"{prefix2}delta"));
            Assert.True(_combinedDict.ContainsKey($"{prefix2}echo"));
            Assert.True(_combinedDict.ContainsKey($"{prefix2}foxtrot"));
        }

        [Fact]
        public void ContainsKeyReturnsFalseWhenAbsent()
        {
            Assert.False(_combinedDict.ContainsKey($"{prefix2}beta"));
            Assert.False(_combinedDict.ContainsKey($"{prefix2}gamma"));
            Assert.False(_combinedDict.ContainsKey($"{prefix2}epsilon"));

            Assert.False(_combinedDict.ContainsKey($"{prefix1}bravo"));
            Assert.False(_combinedDict.ContainsKey($"{prefix1}charlie"));
            Assert.False(_combinedDict.ContainsKey($"{prefix1}echo"));
            Assert.False(_combinedDict.ContainsKey($"{prefix1}foxtrot"));
        }

        [Fact]
        public void EnumerationIncludesOnlyPairsInSeparateDictionaries()
        {
            foreach (KeyValuePair<string, int> kvp in _combinedDict)
            {
                if (kvp.Key.StartsWith(prefix1))
                {
                    Assert.True(_dict1.TryGetValue(kvp.Key.Substring(prefix1.Length), out int value));
                    Assert.Equal(value, kvp.Value);
                }
                else if (kvp.Key.StartsWith(prefix2))
                {
                    Assert.True(_dict2.TryGetValue(kvp.Key.Substring(prefix2.Length), out int value));
                    Assert.Equal(value, kvp.Value);
                }
                else
                {
                    Assert.Fail($"de-prefixed key `{kvp.Key}` not found in either dictionary");
                }
            }
        }

        [Fact]
        public void CountMatchesEnumerationCount()
        {
            Assert.Equal(_combinedDict.Count, _combinedDict.Count());
        }

        [Fact]
        public void TryGetValuReturnsTrueWhenPresent()
        {
            int value;

            Assert.True(_combinedDict.TryGetValue($"{prefix1}alpha", out value));
            Assert.Equal(1, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix1}beta", out value));
            Assert.Equal(3, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix1}gamma", out value));
            Assert.Equal(5, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix1}delta", out value));
            Assert.Equal(7, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix1}epsilon", out value));
            Assert.Equal(9, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}alpha", out value));
            Assert.Equal(1, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}bravo", out value));
            Assert.Equal(1, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}charlie", out value));
            Assert.Equal(2, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}delta", out value));
            Assert.Equal(3, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}echo", out value));
            Assert.Equal(5, value);

            Assert.True(_combinedDict.TryGetValue($"{prefix2}foxtrot", out value));
            Assert.Equal(8, value);
        }

        [Fact]
        public void TryGetValuReturnsFalseWhenAbsent()
        {
            int value;

            Assert.False(_combinedDict.TryGetValue($"{prefix2}beta", out value));
            Assert.False(_combinedDict.TryGetValue($"{prefix2}gamma", out value));
            Assert.False(_combinedDict.TryGetValue($"{prefix2}epsilon", out value));

            Assert.False(_combinedDict.TryGetValue($"{prefix1}bravo", out value));
            Assert.False(_combinedDict.TryGetValue($"{prefix1}charlie", out value));
            Assert.False(_combinedDict.TryGetValue($"{prefix1}echo", out value));
            Assert.False(_combinedDict.TryGetValue($"{prefix1}foxtrot", out value));
        }
    }
}
