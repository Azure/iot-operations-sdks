// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Connection;
namespace Azure.Iot.Operations.Protocol.UnitTests.Connection
{
    public class StringToDictionaryExtensionTests
    {
        [Fact]
        public void ConvertDelimitedStringToDictionary()
        {
            string delimitedString = "MyKey1=myvalue1;MyKey2=myvalue2";
            IDictionary<string, string> map = delimitedString.ToDictionary(';', '=');
            Assert.Equal(2, map.Count);
            Assert.True(map.TryGetValue("MyKey1", out string? value1));
            Assert.NotNull(value1);
            Assert.Equal("myvalue1", value1);
            Assert.True(map.TryGetValue("MyKey2", out string? value2));
            Assert.NotNull(value2);
            Assert.Equal("myvalue2", value2);
        }

        [Fact]
        public void ConvertDelimitedStringToDictionary_WrongDelimiter_ReturnsSingleEntry()
        {
            string delimitedString = "MyKey1=myvalue1;MyKey2=myvalue2";
            IDictionary<string, string> map = delimitedString.ToDictionary(':', '=');
            Assert.Single(map);
            Assert.True(map.TryGetValue("MyKey1", out string? value1));
            Assert.NotNull(value1);
            Assert.Equal("myvalue1;MyKey2=myvalue2", value1);
        }

        [Fact]
        public void ConvertDelimitedStringToDictionary_WrongSeparator_ReturnsEmptyDictionary()
        {
            string delimitedString = "MyKey1=myvalue1;MyKey2=myvalue2";
            IDictionary<string, string> map = delimitedString.ToDictionary(':', ':');
            Assert.Empty(map);
        }

        [Fact]
        public void ConvertEmpty_ReturnsEmptyDictionary()
        {
            string delimitedString = "";
            IDictionary<string, string> map = delimitedString.ToDictionary(':', ':');
            Assert.Empty(map);
        }
    }
}
