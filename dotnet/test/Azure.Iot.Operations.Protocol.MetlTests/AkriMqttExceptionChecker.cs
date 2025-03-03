// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System.Xml;
using Xunit;

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public static class AkriMqttExceptionChecker
    {
        public static void CheckException(TestCaseCatch testCaseCatch, AkriMqttException exception)
        {
            Assert.Equal(testCaseCatch.GetErrorKind(), exception.Kind);

            if (testCaseCatch.InApplication != null)
            {
                Assert.Equal(testCaseCatch.InApplication, exception.InApplication);
            }

            if (testCaseCatch.IsShallow != null)
            {
                Assert.Equal(testCaseCatch.IsShallow, exception.IsShallow);
            }

            if (testCaseCatch.IsRemote != null)
            {
                Assert.Equal(testCaseCatch.IsRemote, exception.IsRemote);
            }

            if (testCaseCatch.StatusCode == null)
            {
                Assert.Null(exception.HttpStatusCode);
            }
            else if (testCaseCatch.StatusCode is int statusCode || int.TryParse(testCaseCatch.StatusCode.ToString(), out statusCode))
            {
                Assert.Equal(statusCode, exception.HttpStatusCode);
            }

            if (testCaseCatch.Message != null)
            {
                Assert.Equal(testCaseCatch.Message, exception.Message);
            }
            if (testCaseCatch.Supplemental == null)
            {
                return;
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.HeaderNameKey, out string? headerName))
            {
                Assert.Equal(headerName, exception.HeaderName);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.HeaderValueKey, out string? headerValue))
            {
                Assert.Equal(headerValue, exception.HeaderValue);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.TimeoutNameKey, out string? timeoutName))
            {
                Assert.Equal(timeoutName, exception.TimeoutName?.ToLower());
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.TimeoutValueKey, out string? timeoutValue))
            {
                Assert.Equal(timeoutValue, exception.TimeoutValue != null ? XmlConvert.ToString((TimeSpan)exception.TimeoutValue) : null);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.PropertyNameKey, out string? propertyName))
            {
                Assert.Equal(propertyName, exception.PropertyName != null ? exception.PropertyName.Substring(exception.PropertyName.LastIndexOf('.') + 1).ToLower() : null);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.PropertyValueKey, out string? propertyValue))
            {
                Assert.Equal(propertyValue, exception.PropertyValue?.ToString());
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.CommandNameKey, out string? commandName))
            {
                Assert.Equal(commandName, exception.CommandName);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.RequestProtocolKey, out string? requestProtocolVersion))
            {
                Assert.Equal(requestProtocolVersion, exception.ProtocolVersion);
            }

            if (testCaseCatch.Supplemental.TryGetValue(TestCaseCatch.SupportedMajorProtocolVersions, out string? supportedMajorProtocolVersions))
            {
                Assert.True(ProtocolVersion.TryParseFromString(supportedMajorProtocolVersions!, out int[] ? intValues), "Could not parse the supported major protocol version value string");
                Assert.True(Enumerable.SequenceEqual(intValues, exception.SupportedMajorProtocolVersions!));
            }
        }
    }
}
