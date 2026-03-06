// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public partial class WotDataSchemaPrimitive : WotDataSchema
    {
        private static readonly Dictionary<int, (string, string)[]> NodeIndexToKvpListMap = new()
        {
            { 1, new[] { ("type", "\"boolean\"") } },
            { 2, new[] { ("type", "\"integer\""), ("minimum", "-128"), ("maximum", "127") } },
            { 3, new[] { ("type", "\"integer\""), ("minimum", "0"), ("maximum", "255") } },
            { 4, new[] { ("type", "\"integer\""), ("minimum", "-32768"), ("maximum", "32767") } },
            { 5, new[] { ("type", "\"integer\""), ("minimum", "0"), ("maximum", "65535") } },
            { 6, new[] { ("type", "\"integer\""), ("minimum", "-2147483648"), ("maximum", "2147483647") } },
            { 7, new[] { ("type", "\"integer\""), ("minimum", "0"), ("maximum", "4294967295") } },
            { 8, new[] { ("type", "\"integer\""), ("minimum", "-9223372036854775808"), ("maximum", "9223372036854775807") } },
            { 9, new[] { ("type", "\"integer\""), ("minimum", "0"), ("maximum", "18446744073709551615") } },
            { 10, new[] { ("type", "\"number\""), ("minimum", "-3.40e+38"), ("maximum", "3.40e+38") } },
            { 11, new[] { ("type", "\"number\""), ("minimum", "-1.80e+308"), ("maximum", "1.80e+308") } },
            { 12, new[] { ("type", "\"string\"") } },
            { 13, new[] { ("type", "\"string\""), ("format", "\"date-time\"") } },
            { 14, new[] { ("type", "\"string\""), ("format", "\"uuid\"") } },
            { 15, new[] { ("type", "\"string\""), ("contentEncoding", "\"base64\"") } },
            { 24, new[] { ("type", "\"string\"") } },
            { 26, new[] { ("type", "\"number\"") } },
            { 27, new[] { ("type", "\"integer\"") } },
            { 28, new[] { ("type", "\"integer\""), ("minimum", "0") } },
            { 290, new[] { ("type", "\"string\""), ("pattern", @"""^P(?!$)(?:(?:(?:(?:\\d+Y)|(?:\\d+\\.\\d+Y$))?(?:(?:\\d+M)|(?:\\d+\\.\\d+M$))?)|(?:(?:(?:\\d+W)|(?:\\d+\\.\\d+W$))?))(?:(?:\\d+D)|(?:\\d+\\.\\d+D$))?(?:T(?!$)(?:(?:\\d+H)|(?:\\d+\\.\\d+H$))?(?:(?:\\d+M)|(?:\\d+\\.\\d+M$))?(?:\\d+(?:\\.\\d+)?S)?)?$""") } },
            { 294, new[] { ("type", "\"string\""), ("format", "\"time\"") } },
            { 12878, new[] { ("type", "\"string\""), ("pattern", @"""^(?:\\+|-)?(?:[1-9][0-9]*|0)(?:\\.[0-9]*)?$""") } },
        };

        private string? description;
        private string? schemaName;
        private string? typeRef;
        private (string, string)[] kvpList;

        public static bool IsPrimitive(OpcUaNodeId dataTypeNodeId)
        {
            return dataTypeNodeId.NsIndex == 0 && NodeIndexToKvpListMap.ContainsKey(dataTypeNodeId.NodeIndex);
        }

        public WotDataSchemaPrimitive(OpcUaNodeId dataTypeNodeId, string? description, string? schemaName = null, OpcUaNode? dataTypeNode = null)
        {
            this.description = description;
            this.schemaName = schemaName;
            this.typeRef = dataTypeNode != null ? $"nsu={dataTypeNode.NodeIdNamespace};i={dataTypeNode.NodeId.NodeIndex}" : null;
            this.kvpList = NodeIndexToKvpListMap[dataTypeNodeId.NodeIndex];
        }
    }
}
