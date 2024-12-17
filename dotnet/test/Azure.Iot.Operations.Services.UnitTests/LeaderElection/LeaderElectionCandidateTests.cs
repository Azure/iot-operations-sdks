// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.LeasedLock;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore.LeaderElection
{
    public class LeaderElectionCandidateTests
    {
        [Fact]
        public void TestConstructorOverloads()
        {
            string value = "someValue";
            var value1 = new LeaderElectionCandidate(Encoding.UTF8.GetBytes(value));
            var value2 = new LeaderElectionCandidate(value);

            Assert.Equal(value1, value2);
        }

        [Fact]
        public void GetStringReturnsUtf8Encoded()
        {
            string value = "someKey!#$!^!&!$%$";
            var key1 = new LeaderElectionCandidate(Encoding.UTF8.GetBytes(value));

            Assert.Equal(value, key1.GetString());
        }

        [Fact]
        public void TestEmptyValue()
        {
            LeaderElectionCandidate value = string.Empty!;
            Assert.Empty(value.Bytes);
            Assert.Equal("", value.GetString());
        }

        [Fact]
        public void StringComparisonWorks()
        {
            string value = "someString";
            LeaderElectionCandidate leaderElectionCandidate = new LeaderElectionCandidate(value);
            Assert.Equal(value, leaderElectionCandidate);
            Assert.Equal(leaderElectionCandidate, value);
            Assert.True(leaderElectionCandidate.Equals(value));
        }
    }
}
