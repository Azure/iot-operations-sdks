using System.Text;
using Azure.Iot.Operations.Services.StateStore;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore
{
    public class StateStoreKeyTests
    {
        [Fact]
        public void TestConstructorOverloads()
        {
            string value = "someKey";
            var key1 = new StateStoreKey(Encoding.UTF8.GetBytes(value));
            var key2 = new StateStoreKey(value);
            var key3 = new StateStoreKey(new MemoryStream(Encoding.UTF8.GetBytes(value)));
            var key4 = new StateStoreKey(new MemoryStream(Encoding.UTF8.GetBytes(value)), value.Length);
            var key5 = new StateStoreKey(new ArraySegment<byte>(Encoding.UTF8.GetBytes(value)));
            var key6 = new StateStoreKey(new List<byte>(Encoding.UTF8.GetBytes(value)));

            Assert.Equal(key1, key2);
            Assert.Equal(key1, key3);
            Assert.Equal(key1, key4);
            Assert.Equal(key1, key5);
            Assert.Equal(key1, key6);
        }

        [Fact]
        public void GetStringReturnsUtf8Encoded()
        {
            string value = "someKey!#$!^!&!$%$";
            var key1 = new StateStoreKey(Encoding.UTF8.GetBytes(value));

            Assert.Equal(value, key1.GetString());
        }

        [Fact]
        public void StringComparisonWorks()
        {
            string value = "someString";
            StateStoreKey stateStoreKey = new StateStoreKey(value);
            Assert.Equal(value, stateStoreKey);
            Assert.Equal(stateStoreKey, value);
            Assert.True(stateStoreKey.Equals(value));
        }

        [Fact]
        public void ConstructorOnlyTakesSegmentOfArraySegment()
        {
            byte[] wholeArray = [0, 1, 2, 3];
            var arraySegment = new ArraySegment<byte>(wholeArray, 1, 2);
            var key = new StateStoreKey(arraySegment);

            Assert.NotNull(key.Bytes);
            Assert.Equal(2, key.Bytes.Length);
            Assert.Equal(1, key.Bytes[0]);
            Assert.Equal(2, key.Bytes[1]);
        }
    }
}
