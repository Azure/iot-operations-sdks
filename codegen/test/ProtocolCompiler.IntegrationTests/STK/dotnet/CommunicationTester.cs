namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.STK
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Azure.Iot.Operations.Protocol.RPC;

    public static class CommunicationTester
    {
        static readonly TimeSpan CommandTimeoutShort = TimeSpan.FromSeconds(2);
        static readonly TimeSpan CommandTimeoutLong = TimeSpan.FromMinutes(10);

        public static void ExecuteTest(string testText, IClientShim clientShim, IServiceShim serviceShim, string modelId)
        {
            JToken testToken;
            using (JsonReader reader = new JsonTextReader(new StringReader(testText)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                testToken = JToken.Load(reader);
            }

            if (testToken is JObject testCaseObject)
            {
                ExecuteTestCase(testCaseObject, clientShim, serviceShim, modelId);
            }
            else if (testToken is JArray testCaseArray)
            {
                foreach (JToken testCaseToken in testCaseArray)
                {
                    ExecuteTestCase((JObject)testCaseToken, clientShim, serviceShim, modelId);
                }
            }
            else
            {
                Assert.Fail("Invalid test text, neither JSON object nor array");
            }
        }

        public static void ExecuteTestCase(JObject testCaseObject, IClientShim clientShim, IServiceShim serviceShim, string modelId)
        {
            if (!testCaseObject.TryGetValue("model", out JToken? modelToken))
            {
                Assert.Fail("Invalid test case, no 'model' property present");
            }
            else if (modelToken is not JValue modelValue || modelValue.Type != JTokenType.String)
            {
                Assert.Fail("Invalid test case, 'model' property is not JSON string");
            }
            else if (modelValue.Value<string>() != modelId)
            {
                Assert.Fail($"Invalid test case, model {modelValue.Value<string>()} not accepted by this generated test infrastructure; only model {modelId} supported");
            }
            else if (!testCaseObject.TryGetValue("pattern", out JToken? patternToken))
            {
                Assert.Fail("Invalid test case, no 'pattern' property present");
            }
            else if (patternToken is not JValue patternValue || patternValue.Type != JTokenType.String)
            {
                Assert.Fail("Invalid test case, 'pattern' property is not JSON string");
            }
            else
            {
                switch (patternValue.Value<string>())
                {
                    case "command":
                        TestCommand(testCaseObject, clientShim, serviceShim);
                        break;
                    case "telemetry":
                        TestTelemetry(testCaseObject, clientShim, serviceShim);
                        break;
                    default:
                        Assert.Fail($"Invalid test case, unrecognized 'pattern' property value '{patternValue.Value<string>()}'");
                        break;
                }
            }
        }

        private static void TestCommand(JObject testCaseObject, IClientShim clientShim, IServiceShim serviceShim)
        {
            if (!testCaseObject.TryGetValue("name", out JToken? nameToken))
            {
                Assert.Fail("Invalid Command test case, no 'name' property present");
            }
            else if (nameToken is not JValue nameValue || nameValue.Type != JTokenType.String)
            {
                Assert.Fail("Invalid Command test case, 'name' property is not JSON string");
            }
            else if (!testCaseObject.TryGetValue("request", out JToken? requestToken))
            {
                Assert.Fail("Invalid Command test case, no 'request' property present");
            }
            else if (!testCaseObject.TryGetValue("response", out JToken? responseToken))
            {
                Assert.Fail("Invalid Command test case, no 'response' property present");
            }
            else
            {
                string commandName = nameValue.Value<string>()!;

                serviceShim.ClearHandlers();
                serviceShim.RegisterHandler(commandName, (request, reqMeta) =>
                {
                    Assert.AreEqual(requestToken.ToString(), request.ToString());
                    Assert.IsFalse(reqMeta.UserData.ContainsKey(RpcUserProperty.ReqVal1));
                    Assert.IsFalse(reqMeta.UserData.ContainsKey(RpcUserProperty.ReqVal2));
                    return new ExtendedResponse<JToken> { Response = responseToken, ResponseMetadata = new CommandResponseMetadata() };
                });

                JToken response = clientShim.InvokeCommand(commandName, requestToken, CommandTimeoutLong).Result;
                Assert.AreEqual(responseToken.ToString(), response.ToString());

                if (testCaseObject.TryGetValue("requestMeta", out JToken? reqMetaToken) && testCaseObject.TryGetValue("responseMeta", out JToken? respMetaToken))
                {
                    CommandRequestMetadata requestMetadata = new();
                    LoadUserMetadata(reqMetaToken, requestMetadata.UserData);

                    CommandResponseMetadata responseMetadata = new();
                    LoadUserMetadata(respMetaToken, responseMetadata.UserData);

                    serviceShim.RegisterHandler(commandName, (request, reqMeta) =>
                    {
                        Assert.AreEqual(requestToken.ToString(), request.ToString());
                        Assert.AreEqual(requestMetadata.UserData[RpcUserProperty.ReqVal1], reqMeta.UserData[RpcUserProperty.ReqVal1]);
                        Assert.AreEqual(requestMetadata.UserData[RpcUserProperty.ReqVal2], reqMeta.UserData[RpcUserProperty.ReqVal2]);
                        return new ExtendedResponse<JToken> { Response = responseToken, ResponseMetadata = responseMetadata };
                    });

                    ExtendedResponse<JToken> extended = clientShim.InvokeCommand(commandName, requestToken, requestMetadata, CommandTimeoutLong).WithMetadata().Result;
                    Assert.AreEqual(responseToken.ToString(), extended.Response?.ToString());
                    Assert.AreEqual(responseMetadata.UserData[RpcUserProperty.ReqVal1], extended.ResponseMetadata?.UserData[RpcUserProperty.ReqVal1]);
                    Assert.AreEqual(responseMetadata.UserData[RpcUserProperty.ReqVal2], extended.ResponseMetadata?.UserData[RpcUserProperty.ReqVal2]);
                }
            }
        }

        private static void TestTelemetry(JObject testCaseObject, IClientShim clientShim, IServiceShim serviceShim)
        {
            string telemetryName = string.Empty;
            if (testCaseObject.TryGetValue("name", out JToken? nameToken))
            {
                if (nameToken is not JValue nameValue || nameValue.Type != JTokenType.String)
                {
                    Assert.Fail("Invalid Telemetry test case, 'name' property is not JSON string");
                }
                else
                {
                    telemetryName = nameValue.Value<string>()!;
                }
            }

            if (!testCaseObject.TryGetValue("value", out JToken? valueToken))
            {
                Assert.Fail("Invalid Telemetry test case, no 'value' property present");
            }
            else
            {

                clientShim.ClearHandlers();
                clientShim.RegisterHandler(telemetryName, (sender, value) =>
                {
                    Assert.AreEqual(valueToken.ToString(), value.ToString());
                });

                serviceShim.SendTelemetry(telemetryName, valueToken);
            }
        }

        private static void LoadUserMetadata(JToken metaToken, Dictionary<string, string> userData)
        {
            if (metaToken is JObject metaObject)
            {
                if (!metaObject.TryGetValue("val1", out JToken? val1Token))
                {
                    throw new Exception("Invalid test case, no 'val1' property present");
                }
                else if (val1Token is not JValue val1Value || val1Value.Type != JTokenType.String)
                {
                    throw new Exception("Invalid test case, 'val1' property is not JSON string");
                }
                else
                {
                    userData[RpcUserProperty.ReqVal1] = val1Value.Value<string>()!;
                }

                if (!metaObject.TryGetValue("val2", out JToken? val2Token))
                {
                    throw new Exception("Invalid test case, no 'val2' property present");
                }
                else if (val2Token is not JValue val2Value || val2Value.Type != JTokenType.String)
                {
                    throw new Exception("Invalid test case, 'val2' property is not JSON string");
                }
                else
                {
                    userData[RpcUserProperty.ReqVal2] = val2Value.Value<string>()!;
                }
            }
            else
            {
                throw new Exception("Invalid Command test case, 'requestMeta' property is not JSON object");
            }
        }
    }
}
