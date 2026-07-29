// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    public static class JsonSchemaValues
    {
        public const string PropertyAdditionalProperties = "additionalProperties";
        public const string PropertyAnyOf = "anyOf";
        public const string PropertyContentEncoding = "contentEncoding";
        public const string PropertyDescription = "description";
        public const string PropertyEnum = "enum";
        public const string PropertyFormat = "format";
        public const string PropertyItems = "items";
        public const string PropertyMaximum = "maximum";
        public const string PropertyMinimum = "minimum";
        public const string PropertyPattern = "pattern";
        public const string PropertyProperties = "properties";
        public const string PropertyRef = "$ref";
        public const string PropertyRequired = "required";
        public const string PropertySchema = "$schema";
        public const string PropertyTitle = "title";
        public const string PropertyType = "type";

        public const string TypeArray = "array";
        public const string TypeInteger = "integer";
        public const string TypeNumber = "number";
        public const string TypeObject = "object";
        public const string TypeString = "string";
        public const string TypeBoolean = "boolean";
        public const string TypeNull = "null";

        public const string FormatDouble = "double";
        public const string FormatFloat = "float";

        public const string FormatDate = "date";
        public const string FormatDateTime = "date-time";
        public const string FormatDuration = "duration";
        public const string FormatTime = "time";
        public const string FormatUuid = "uuid";

        public const string ContentEncodingBase64 = "base64";

        public const string PatternDecimal = "^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$";

        public static readonly string[] InternalDefsKeys = { "$defs", "definitions" };
    }
}
