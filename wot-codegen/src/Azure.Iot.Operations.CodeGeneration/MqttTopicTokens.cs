// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    /// <summary>
    /// Static class that defines string values of the replaceable components used in topic patterns.
    /// </summary>
    public static class MqttTopicTokens
    {
        /// <summary>
        /// Prefix for a custom token.
        /// </summary>
        public const string PrefixCustom = "ex:";

        /// <summary>
        /// Token representing the ID of an Action executor, should be used only in Action topic patterns.
        /// </summary>
        public const string ActionExecutorId = "executorId";

        /// <summary>
        /// Token representing the MQTT Client ID of an Action invoker, should be used only in Action topic patterns.
        /// </summary>
        public const string ActionInvokerId = "invokerClientId";

        /// <summary>
        /// Token representing the ID of an Event sender, should be used only in Event topic patterns.
        /// </summary>
        public const string EventSenderId = "senderId";

        /// <summary>
        /// Token representing the ID of a Property maintainer, should be used only in Property topic patterns.
        /// </summary>
        public const string PropertyMaintainerId = "maintainerId";

        /// <summary>
        /// Token representing the MQTT Client ID of a Property consumer, should be used only in Property topic patterns.
        /// </summary>
        public const string PropertyConsumerId = "consumerClientId";

        /// <summary>
        /// Token representing a Property action, 'read' or 'write'.
        /// </summary>
        public const string PropertyAction = "action";

        public static class PropertyActionValues
        {
            /// <summary>
            /// Token value indicating a Property read action, 'readproperty' or 'readallproperties'.
            /// </summary>
            public const string Read = "read";

            /// <summary>
            /// Token value indicating a Property write action, 'writeproperty' or 'writemultipleproperties'.
            /// </summary>
            public const string Write = "write";
        }
    }
}
