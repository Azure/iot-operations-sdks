// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Services.LeasedLock;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore.LeasedLock
{
    public class LeasedLockHolderTests
    {
        [Fact]
        public void TestConstructorOverloads()
        {
            string value = "someValue";
            var value1 = new LeasedLockHolder(Encoding.UTF8.GetBytes(value));
            var value2 = new LeasedLockHolder(value);

            Assert.Equal(value1, value2);
        }

        [Fact]
        public void GetStringReturnsUtf8Encoded()
        {
            string value = "someKey!#$!^!&!$%$";
            var key1 = new LeasedLockHolder(Encoding.UTF8.GetBytes(value));

            Assert.Equal(value, key1.GetString());
        }

        [Fact]
        public void TestEmptyValue()
        {
            LeasedLockHolder value = string.Empty!;
            Assert.Empty(value.Bytes);
            Assert.Equal("", value.GetString());
        }

        [Fact]
        public void StringComparisonWorks()
        {
            string value = "someString";
            LeasedLockHolder leasedLockHolder = new LeasedLockHolder(value);
            Assert.Equal(value, leasedLockHolder);
            Assert.Equal(leasedLockHolder, value);
            Assert.True(leasedLockHolder.Equals(value));
        }
    }
}
