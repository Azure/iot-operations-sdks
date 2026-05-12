// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Thrown by an <see cref="IManagementActionHandler"/> method to signal that the handler
    /// is not wired up to service this action type (for example, a handler implementing only
    /// <see cref="IManagementActionHandler.HandleCallAsync"/> that has
    /// <see cref="IManagementActionHandler.HandleReadAsync"/> invoked on it).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base <see cref="ManagementActionConnectorWorker"/> catches this exception specifically
    /// and translates it into a <see cref="ManagementActionApplicationError"/> with error code
    /// <c>UnsupportedActionType</c> so the invoker sees a meaningful failure rather than a
    /// generic internal error.
    /// </para>
    /// <para>
    /// This is distinct from <see cref="NotSupportedException"/> so that the worker can
    /// distinguish "the handler intentionally declined this call" from a generic
    /// <see cref="NotSupportedException"/> bubbling up out of the handler's own logic
    /// (which is translated to <c>InternalError</c>).
    /// </para>
    /// </remarks>
    public sealed class ManagementActionNotSupportedException : Exception
    {
        /// <summary>The management group name the unsupported invocation targeted.</summary>
        public string GroupName { get; }

        /// <summary>The management action name the unsupported invocation targeted.</summary>
        public string ActionName { get; }

        public ManagementActionNotSupportedException(string groupName, string actionName)
            : base($"Handler does not support this action type for '{groupName}::{actionName}'.")
        {
            GroupName = groupName;
            ActionName = actionName;
        }

        public ManagementActionNotSupportedException(string groupName, string actionName, string message)
            : base(message)
        {
            GroupName = groupName;
            ActionName = actionName;
        }

        public ManagementActionNotSupportedException(string groupName, string actionName, string message, Exception innerException)
            : base(message, innerException)
        {
            GroupName = groupName;
            ActionName = actionName;
        }
    }
}

