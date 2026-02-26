// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;

    public static class TDValues
    {
        public const string ContextUriWotTd = "https://www.w3.org/2022/wot/td/v1.1";
        public const string ContextUriAioProtocol = "http://azure.com/DigitalTwins/dtmi#";
        public const string ContextUriAioPlatform = "http://azure.com/IoT/operations/tm#";
        public const string ContextUriQudt = "http://qudt.org/schema/qudt/";
        public const string ContextPrefixAioProtocol = "dtv";
        public const string ContextPrefixAioPlatform = "aov";
        public const string ContextPrefixQudt = "qudt";

        public const string RelationExtends = "tm:extends";
        public const string RelationReference = "aov:reference";
        public const string RelationTypedReference = "aov:typedReference";
        public const string RelationCapability = "aov:capability";
        public const string RelationComponent = "aov:component";
        public const string RelationSchemaNaming = "dtv:naming";

        public const string ContentTypeTmJson = "application/tm+json";
        public const string ContentTypeJson = "application/json";
        public const string ContentTypeRaw = "application/octet-stream";
        public const string ContentTypeCustom = "";

        public const string TypeThingModel = "tm:ThingModel";

        public const string OpInvokeAction = "invokeaction";
        public const string OpReadProp = "readproperty";
        public const string OpWriteProp = "writeproperty";
        public const string OpSubEvent = "subscribeevent";
        public const string OpReadAllProps = "readallproperties";
        public const string OpWriteMultProps = "writemultipleproperties";
        public const string OpSubAllEvents = "subscribeallevents";

        public const string TypeObject = "object";
        public const string TypeArray = "array";
        public const string TypeString = "string";
        public const string TypeNumber = "number";
        public const string TypeInteger = "integer";
        public const string TypeBoolean = "boolean";
        public const string TypeNull = "null";

        public const string FormatDateTime = "date-time";
        public const string FormatDate = "date";
        public const string FormatTime = "time";
        public const string FormatUuid = "uuid";

        public const string ContentEncodingBase64 = "base64";

        public static readonly HashSet<string> OpValues = new HashSet<string>() {
            OpReadProp,
            OpWriteProp,
            OpReadAllProps,
            OpWriteMultProps,
            OpSubAllEvents,
        };

        public static readonly HashSet<string> TypeValues = new HashSet<string>() {
            TypeObject,
            TypeArray,
            TypeString,
            TypeNumber,
            TypeInteger,
            TypeBoolean,
            TypeNull,
        };

        public static readonly HashSet<string> FormatValues = new HashSet<string>() {
            FormatDateTime,
            FormatDate,
            FormatTime,
            FormatUuid,
        };
    }
}
