// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib.UnitTests
{
    using System;
    using Azure.Iot.Operations.Opc2WotLib;
    using Xunit;

    public class WotUtilTests
    {
        [Theory]
        [InlineData("http://example.test:4840/UA/", "http://example.test:4841/UA/")]
        [InlineData("http://example.test/UA", "http://example.test/UA/")]
        [InlineData("http://example.test/UA/?version=1", "http://example.test/UA/?version=2")]
        [InlineData("http://example.test/UA/#one", "http://example.test/UA/#two")]
        public void GetTypeRef_DistinguishesCompleteNamespaceUris(string firstNamespaceUri, string secondNamespaceUri)
        {
            Assert.NotEqual(
                WotUtil.GetTypeRef(firstNamespaceUri, "Thing"),
                WotUtil.GetTypeRef(secondNamespaceUri, "Thing"));
        }

        [Fact]
        public void GetThingModelId_EncodesUnsafeTypeReferenceCharacters()
        {
            string typeRef = WotUtil.GetTypeRef("http://example.test/UA/", "Thing%#");
            string id = WotUtil.GetThingModelId(typeRef);

            Assert.DoesNotContain('#', typeRef);
            Assert.Contains("%25%23", typeRef);
            Assert.True(Uri.TryCreate(id, UriKind.Absolute, out Uri? uri));
            Assert.Equal(id, uri.AbsoluteUri);
        }
    }
}
