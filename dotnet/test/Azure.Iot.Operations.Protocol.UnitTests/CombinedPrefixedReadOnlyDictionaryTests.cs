using Azure.Iot.Operations.Protocol.UnitTests.Support;

namespace Azure.Iot.Operations.Protocol.UnitTests
{
    public class CombinedPrefixedReadOnlyDictionaryTests
    {
        private const string prefix1 = "foo:";
        private const string prefix2 = "bar:";

        private Dictionary<string, int> dict1;
        private Dictionary<string, int> dict2;
        private IReadOnlyDictionary<string, int> combinedDict;

        public CombinedPrefixedReadOnlyDictionaryTests()
        {
            dict1 = new()
            {
                { "alpha", 1 },
                { "beta", 3 },
                { "gamma", 5 },
                { "delta", 7 },
                { "epsilon", 9 },
            };

            dict2 = new()
            {
                { "alpha", 1 },
                { "bravo", 1 },
                { "charlie", 2 },
                { "delta", 3 },
                { "echo", 5 },
                { "foxtrot", 8 },
            };

            combinedDict = new CombinedPrefixedReadOnlyDictionary<int>(prefix1, dict1, prefix2, dict2);
        }

        [Fact]
        public void KeysEnumerationIncludesAll()
        {
            Assert.Contains($"{prefix1}alpha", combinedDict.Keys);
            Assert.Contains($"{prefix1}beta", combinedDict.Keys);
            Assert.Contains($"{prefix1}gamma", combinedDict.Keys);
            Assert.Contains($"{prefix1}delta", combinedDict.Keys);
            Assert.Contains($"{prefix1}epsilon", combinedDict.Keys);

            Assert.Contains($"{prefix2}alpha", combinedDict.Keys);
            Assert.Contains($"{prefix2}bravo", combinedDict.Keys);
            Assert.Contains($"{prefix2}charlie", combinedDict.Keys);
            Assert.Contains($"{prefix2}delta", combinedDict.Keys);
            Assert.Contains($"{prefix2}echo", combinedDict.Keys);
            Assert.Contains($"{prefix2}foxtrot", combinedDict.Keys);
        }

        [Fact]
        public void KeysEnumerationNotPolluted()
        {
            Assert.DoesNotContain($"{prefix2}beta", combinedDict.Keys);
            Assert.DoesNotContain($"{prefix2}gamma", combinedDict.Keys);
            Assert.DoesNotContain($"{prefix2}epsilon", combinedDict.Keys);

            Assert.DoesNotContain($"{prefix1}bravo", combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}charlie", combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}echo", combinedDict.Keys);
            Assert.DoesNotContain($"{prefix1}foxtrot", combinedDict.Keys);
        }

        [Fact]
        public void ValuesEnumerationIncludesUnion()
        {
            Assert.Contains(1, combinedDict.Values);
            Assert.Contains(2, combinedDict.Values);
            Assert.Contains(3, combinedDict.Values);
            Assert.Contains(5, combinedDict.Values);
            Assert.Contains(7, combinedDict.Values);
            Assert.Contains(8, combinedDict.Values);
            Assert.Contains(9, combinedDict.Values);
        }

        [Fact]
        public void CountMatchesTotal()
        {
            Assert.Equal(dict1.Count + dict2.Count, ((IReadOnlyDictionary<string, int>)combinedDict).Count);
        }

        [Fact]
        public void LookupFindsAppropriateValue()
        {
            Assert.Equal(1, combinedDict[$"{prefix1}alpha"]);
            Assert.Equal(3, combinedDict[$"{prefix1}beta"]);
            Assert.Equal(5, combinedDict[$"{prefix1}gamma"]);
            Assert.Equal(7, combinedDict[$"{prefix1}delta"]);
            Assert.Equal(9, combinedDict[$"{prefix1}epsilon"]);

            Assert.Equal(1, combinedDict[$"{prefix2}alpha"]);
            Assert.Equal(1, combinedDict[$"{prefix2}bravo"]);
            Assert.Equal(2, combinedDict[$"{prefix2}charlie"]);
            Assert.Equal(3, combinedDict[$"{prefix2}delta"]);
            Assert.Equal(5, combinedDict[$"{prefix2}echo"]);
            Assert.Equal(8, combinedDict[$"{prefix2}foxtrot"]);
        }

        [Fact]
        public void ContainsKeyReturnsTrueWhenPresent()
        {
            Assert.True(combinedDict.ContainsKey($"{prefix1}alpha"));
            Assert.True(combinedDict.ContainsKey($"{prefix1}beta"));
            Assert.True(combinedDict.ContainsKey($"{prefix1}gamma"));
            Assert.True(combinedDict.ContainsKey($"{prefix1}delta"));
            Assert.True(combinedDict.ContainsKey($"{prefix1}epsilon"));

            Assert.True(combinedDict.ContainsKey($"{prefix2}alpha"));
            Assert.True(combinedDict.ContainsKey($"{prefix2}bravo"));
            Assert.True(combinedDict.ContainsKey($"{prefix2}charlie"));
            Assert.True(combinedDict.ContainsKey($"{prefix2}delta"));
            Assert.True(combinedDict.ContainsKey($"{prefix2}echo"));
            Assert.True(combinedDict.ContainsKey($"{prefix2}foxtrot"));
        }

        [Fact]
        public void ContainsKeyReturnsFalseWhenAbsent()
        {
            Assert.False(combinedDict.ContainsKey($"{prefix2}beta"));
            Assert.False(combinedDict.ContainsKey($"{prefix2}gamma"));
            Assert.False(combinedDict.ContainsKey($"{prefix2}epsilon"));

            Assert.False(combinedDict.ContainsKey($"{prefix1}bravo"));
            Assert.False(combinedDict.ContainsKey($"{prefix1}charlie"));
            Assert.False(combinedDict.ContainsKey($"{prefix1}echo"));
            Assert.False(combinedDict.ContainsKey($"{prefix1}foxtrot"));
        }

        [Fact]
        public void EnumerationIncludesOnlyPairsInSeparateDictionaries()
        {
            foreach (KeyValuePair<string, int> kvp in combinedDict)
            {
                if (kvp.Key.StartsWith(prefix1))
                {
                    Assert.True(dict1.TryGetValue(kvp.Key.Substring(prefix1.Length), out int value));
                    Assert.Equal(value, kvp.Value);
                }
                else if (kvp.Key.StartsWith(prefix2))
                {
                    Assert.True(dict2.TryGetValue(kvp.Key.Substring(prefix2.Length), out int value));
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
            Assert.Equal(combinedDict.Count, combinedDict.Count());
        }

        [Fact]
        public void TryGetValuReturnsTrueWhenPresent()
        {
            int value;

            Assert.True(combinedDict.TryGetValue($"{prefix1}alpha", out value));
            Assert.Equal(1, value);

            Assert.True(combinedDict.TryGetValue($"{prefix1}beta", out value));
            Assert.Equal(3, value);

            Assert.True(combinedDict.TryGetValue($"{prefix1}gamma", out value));
            Assert.Equal(5, value);

            Assert.True(combinedDict.TryGetValue($"{prefix1}delta", out value));
            Assert.Equal(7, value);

            Assert.True(combinedDict.TryGetValue($"{prefix1}epsilon", out value));
            Assert.Equal(9, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}alpha", out value));
            Assert.Equal(1, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}bravo", out value));
            Assert.Equal(1, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}charlie", out value));
            Assert.Equal(2, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}delta", out value));
            Assert.Equal(3, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}echo", out value));
            Assert.Equal(5, value);

            Assert.True(combinedDict.TryGetValue($"{prefix2}foxtrot", out value));
            Assert.Equal(8, value);
        }

        [Fact]
        public void TryGetValuReturnsFalseWhenAbsent()
        {
            int value;

            Assert.False(combinedDict.TryGetValue($"{prefix2}beta", out value));
            Assert.False(combinedDict.TryGetValue($"{prefix2}gamma", out value));
            Assert.False(combinedDict.TryGetValue($"{prefix2}epsilon", out value));

            Assert.False(combinedDict.TryGetValue($"{prefix1}bravo", out value));
            Assert.False(combinedDict.TryGetValue($"{prefix1}charlie", out value));
            Assert.False(combinedDict.TryGetValue($"{prefix1}echo", out value));
            Assert.False(combinedDict.TryGetValue($"{prefix1}foxtrot", out value));
        }
    }
}
