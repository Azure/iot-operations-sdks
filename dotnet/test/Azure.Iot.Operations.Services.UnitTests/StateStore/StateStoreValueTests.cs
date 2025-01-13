// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Services.StateStore;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore
{
    public class StateStoreValueTests
    {
        [Fact]
        public void TestConstructorOverloads()
        {
            string value = "someValue";
            var value1 = new StateStoreValue(Encoding.UTF8.GetBytes(value));
            var value2 = new StateStoreValue(value);
            var value3 = new StateStoreValue(new MemoryStream(Encoding.UTF8.GetBytes(value)));
            var value4 = new StateStoreValue(new MemoryStream(Encoding.UTF8.GetBytes(value)), value.Length);
            var value5 = new StateStoreValue(new ArraySegment<byte>(Encoding.UTF8.GetBytes(value)));
            var value6 = new StateStoreValue(new List<byte>(Encoding.UTF8.GetBytes(value)));

            Assert.Equal(value1, value2);
            Assert.Equal(value1, value3);
            Assert.Equal(value1, value4);
            Assert.Equal(value1, value5);
            Assert.Equal(value1, value6);
        }

        [Fact]
        public void GetStringReturnsUtf8Encoded()
        {
            string value = "someKey!#$!^!&!$%$";
            var key1 = new StateStoreKey(Encoding.UTF8.GetBytes(value));

            Assert.Equal(value, key1.GetString());
        }

        [Fact]
        public void TestEmptyValue()
        {
            StateStoreValue value = string.Empty!;
            Assert.Empty(value.Bytes);
            Assert.Equal("", value.GetString());
        }

        [Fact]
        public void StringComparisonWorks()
        {
            string value = "someString";
            StateStoreValue stateStoreValue = new StateStoreValue(value);
            Assert.Equal(value, stateStoreValue);
            Assert.Equal(stateStoreValue, value);
            Assert.True(stateStoreValue.Equals(value));
        }
    }
}
