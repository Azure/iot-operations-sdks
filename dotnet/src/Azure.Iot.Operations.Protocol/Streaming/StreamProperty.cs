// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Streaming
{
    // Builds the "__stream" MQTT user-property value that tags every streaming message.
    // Grammar (ADR 25): d:index[:timeout] | c:index:word[:timeout] | s:index[:timeout].
    internal static class StreamProperty
    {
        public const string Name = "__stream";

        // A data entry at the given index. timeoutSeconds is the invoker's remaining exchange
        // budget and is present on request-direction messages only (omitted on responses).
        public static string Data(uint index, uint? timeoutSeconds = null) =>
            timeoutSeconds is uint t ? $"d:{index}:{t}" : $"d:{index}";

        // The standalone control message that closes a producer's stream.
        public static string Last(uint index, uint? timeoutSeconds = null) =>
            timeoutSeconds is uint t ? $"c:{index}:last:{t}" : $"c:{index}:last";

        // Parses a "__stream" value back into its kind + index (inverse of Data/Last).
        public static bool TryParse(string? value, out StreamTag tag)
        {
            tag = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(':');
            switch (parts[0])
            {
                case "d" when parts.Length is 2 or 3 && uint.TryParse(parts[1], out uint dIndex):
                    tag = new StreamTag(StreamMessageKind.Data, dIndex, ParseTimeout(parts, 2));
                    return true;
                case "c" when parts.Length is 3 or 4 && uint.TryParse(parts[1], out uint cIndex) && TryParseControl(parts[2], out StreamControlCommand command):
                    tag = new StreamTag(StreamMessageKind.Control, cIndex, ParseTimeout(parts, 3), command);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseControl(string word, out StreamControlCommand command)
        {
            switch (word)
            {
                case "last":
                    command = StreamControlCommand.Last;
                    return true;
                case "cancel":
                    command = StreamControlCommand.Cancel;
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        private static uint? ParseTimeout(string[] parts, int index) =>
            parts.Length > index && uint.TryParse(parts[index], out uint t) ? t : null;
    }

    // The three tagged forms a "__stream" value can take (ADR 25).
    internal enum StreamMessageKind
    {
        Data,
        Control,
        Status,
    }

    // Command words carried by the control (c) form.
    internal enum StreamControlCommand
    {
        Last,
        Cancel,
    }

    // A parsed "__stream" tag: the form, its index, the optional request-direction timeout,
    // and (for the control form) which control command it carries.
    internal readonly record struct StreamTag(
        StreamMessageKind Kind,
        uint Index,
        uint? TimeoutSeconds,
        StreamControlCommand? Control = null);
}
