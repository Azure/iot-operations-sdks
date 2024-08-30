using System.Collections.Generic;
using System.Text;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Azure.Iot.Operations.Services.Test.Unit.StateStore
{
    public class StateStorePayloadParserTests
    {
        [Fact]
        public void ParseSetResponseThrowsIfNotSimpleString()
        {
            // arrange
            byte[] invalidSetResponse = Encoding.ASCII.GetBytes("this is not a valid RESP3 simple string");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseSetResponse(invalidSetResponse, new HybridLogicalClock()));
        }

        [Fact]
        public void ParseSetResponseReturnsTrueIfOK()
        {
            // arrange
            byte[] validSetResponse = Encoding.ASCII.GetBytes("+OK\r\n");

            // act, assert
            Assert.True(StateStorePayloadParser.ParseSetResponse(validSetResponse, new HybridLogicalClock()).Success);
        }

        [Fact]
        public void ParseSetResponseThrowsIfTimestampMissingWhenOk()
        {
            // arrange
            byte[] validSetResponse = Encoding.ASCII.GetBytes("+OK\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseSetResponse(validSetResponse, null));
        }

        [Fact]
        public void ParseSetResponseThrowsIfTimestampMissingWhenNil()
        {
            // arrange
            byte[] validSetResponse = Encoding.ASCII.GetBytes("$-1\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseSetResponse(validSetResponse, null));
        }

        [Fact]
        public void ParseSetResponseReturnsFalseIfNotOK()
        {
            // arrange
            byte[] invalidSetResponse = Encoding.ASCII.GetBytes("+not OK\r\n");

            // act, assert
            Assert.False(StateStorePayloadParser.ParseSetResponse(invalidSetResponse, new HybridLogicalClock()).Success);
        }

        [Fact]
        public void ParseSetResponseReturnsFalseIfNil()
        {
            // arrange
            byte[] nilSetResponse = Encoding.ASCII.GetBytes("$-1\r\n");

            // act, assert
            Assert.False(StateStorePayloadParser.ParseSetResponse(nilSetResponse, new HybridLogicalClock()).Success);
        }

        [Fact]
        public void ParseGetResponseThrowsIfNotBlobString()
        {
            // arrange
            byte[] invalidGetResponse = Encoding.ASCII.GetBytes("this is not a valid RESP3 blob string");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseGetResponse(invalidGetResponse));
        }

        [Fact]
        public void ParseGetResponseReturnsNullIfNilPayload()
        {
            // arrange
            byte[] getResponse = Encoding.ASCII.GetBytes("$-1\r\n");

            // act
            byte[]? actual = StateStorePayloadParser.ParseGetResponse(getResponse);
            
            // assert
            Assert.Null(actual);
        }

        [Fact]
        public void ParseGetResponseSuccess()
        {
            // arrange
            byte[] validGetResponse = Encoding.ASCII.GetBytes("$11\r\nhello world\r\n");

            // act, assert
            ReadOnlySpan<byte> getResponse = StateStorePayloadParser.ParseGetResponse(validGetResponse);

            Assert.Equal(Encoding.ASCII.GetBytes("hello world"), getResponse.ToArray());
        }

        [Fact]
        public void ParseGetResponseSuccessEmptyValue()
        {
            // arrange
            byte[] validGetResponse = Encoding.ASCII.GetBytes("$0\r\n\r\n");

            // act, assert
            ReadOnlySpan<byte> getResponse = StateStorePayloadParser.ParseGetResponse(validGetResponse);

            Assert.Equal(0, getResponse.Length);
        }

        [Fact]
        public void ParseDelResponseThrowsIfNotNumber()
        {
            // arrange
            byte[] invalidSetResponse = Encoding.ASCII.GetBytes("this is not a valid RESP3 number");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseDelResponse(invalidSetResponse));
        }

        [Fact]
        public void ParseDelResponseReturnsFalseIfNotOne()
        {
            // arrange
            byte[] invalidSetResponse = Encoding.ASCII.GetBytes(":0\r\n");

            // act, assert
            Assert.Equal(0, StateStorePayloadParser.ParseDelResponse(invalidSetResponse));
        }

        [Fact]
        public void BuildSetRequestSuccess()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] expected = Encoding.ASCII.GetBytes("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");

            // act
            byte[] actual = StateStorePayloadParser.BuildSetRequestPayload(key, value, null);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildGetRequestSuccess()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] expected = Encoding.ASCII.GetBytes("*2\r\n$3\r\nGET\r\n$3\r\nkey\r\n");

            // act
            byte[] actual = StateStorePayloadParser.BuildGetRequestPayload(key);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildDelRequestSuccess()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] expected = Encoding.ASCII.GetBytes("*2\r\n$3\r\nDEL\r\n$3\r\nkey\r\n");

            // act
            byte[] actual = StateStorePayloadParser.BuildDelRequestPayload(key);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildSetRequestPayloadSuccessWithAllOptionalParamsOnlyIfNotSet()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] expected = Encoding.ASCII.GetBytes("*6\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n$2\r\nNX\r\n$2\r\nPX\r\n$4\r\n1000\r\n");

            // act
            byte[] actual = 
                StateStorePayloadParser.BuildSetRequestPayload(
                    key, 
                    value, 
                    new StateStoreSetRequestOptions()
                    { 
                        Condition = SetCondition.OnlyIfNotSet,
                        ExpiryTime = TimeSpan.FromSeconds(1),
                    });

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildSetRequestPayloadSuccessWithAllOptionalParamsOnlyIfEqualOrNotSet()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] expected = Encoding.ASCII.GetBytes("*6\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n$3\r\nNEX\r\n$2\r\nPX\r\n$4\r\n1000\r\n");

            // act
            byte[] actual =
                StateStorePayloadParser.BuildSetRequestPayload(
                    key,
                    value,
                    new StateStoreSetRequestOptions()
                    {
                        Condition = SetCondition.OnlyIfEqualOrNotSet,
                        ExpiryTime = TimeSpan.FromSeconds(1),
                    });

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildSetRequestPayloadSuccessWithEmptyValue()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("");
            byte[] expected = Encoding.ASCII.GetBytes("*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$0\r\n\r\n");

            // act
            byte[] actual =
                StateStorePayloadParser.BuildSetRequestPayload(
                    key,
                    value);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void BuildVDelRequestSuccess()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] expected = Encoding.ASCII.GetBytes("*3\r\n$4\r\nVDEL\r\n$3\r\nkey\r\n$5\r\nvalue\r\n");

            // act
            byte[] actual = StateStorePayloadParser.BuildVDelRequestPayload(key, value);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParseKeyNotificationSuccessSet()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] keyNotificationSetPayload = Encoding.ASCII.GetBytes($"*4\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n${value.Bytes.Length}\r\n{value.GetString()}\r\n");

            // act
            StateStoreKeyNotification keyNotification = StateStorePayloadParser.ParseKeyNotification(keyNotificationSetPayload, key.Bytes);

            // assert
            Assert.Equal(KeyState.Updated, keyNotification.KeyState);
            Assert.Equal(key, keyNotification.Key);
            Assert.Equal(value, keyNotification.Value);
        }

        [Fact]
        public void ParseKeyNotificationDeleteSet()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] keyNotificationDelPayload = Encoding.ASCII.GetBytes("*2\r\n$6\r\nNOTIFY\r\n$6\r\nDELETE\r\n");

            // act
            StateStoreKeyNotification keyNotification = StateStorePayloadParser.ParseKeyNotification(keyNotificationDelPayload, key.Bytes);

            // assert
            Assert.Equal(KeyState.Deleted, keyNotification.KeyState);
            Assert.Equal(key, keyNotification.Key);
        }

        [Fact]
        public void ParseKeyNotificationThrowsIfArrayTooShort()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] keyNotificationInvalidPayload = Encoding.ASCII.GetBytes($"*1\r\n$6\r\nNOTIFY\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseKeyNotification(keyNotificationInvalidPayload, key.Bytes));
        }

        [Fact]
        public void ParseKeyNotificationThrowsIfArraysFirstElementIsNotNotify()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] keyNotificationInvalidPayload = Encoding.ASCII.GetBytes("*2\r\n$9\r\nNotNOTIFY\r\n$6\r\nDELETE\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseKeyNotification(keyNotificationInvalidPayload, key.Bytes));
        }

        [Fact]
        public void ParseKeyNotificationThrowsIfArraysSecondElementIsNotSetOrDelete()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] keyNotificationInvalidPayload = Encoding.ASCII.GetBytes("*2\r\n$6\r\nNOTIFY\r\n$7\r\nUNKNOWN\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseKeyNotification(keyNotificationInvalidPayload, key.Bytes));
        }

        [Fact]
        public void ParseKeyNotificationThrowsIfDeleteAndArrayHasExtraElements()
        {
            // arrange
            var key = new StateStoreKey("key");
            byte[] keyNotificationInvalidDeletePayload = Encoding.ASCII.GetBytes($"*3\r\n$6\r\nNOTIFY\r\n$6\r\nDELETE\r\n$10\r\nExtraStuff\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseKeyNotification(keyNotificationInvalidDeletePayload, key.Bytes));
        }

        [Fact]
        public void ParseKeyNotificationThrowsIfSetAndArrayHasExtraElements()
        {
            // arrange
            var key = new StateStoreKey("key");
            var value = new StateStoreValue("value");
            byte[] keyNotificationInvalidSetPayload = Encoding.ASCII.GetBytes($"*5\r\n$6\r\nNOTIFY\r\n$3\r\nSET\r\n$5\r\nVALUE\r\n${value.Bytes.Length}\r\n{value.GetString()}\r\n$10\r\nExtraStuff\r\n");

            // act, assert
            Assert.Throws<StateStoreOperationException>(() => StateStorePayloadParser.ParseKeyNotification(keyNotificationInvalidSetPayload, key.Bytes));
        }
    }
}
