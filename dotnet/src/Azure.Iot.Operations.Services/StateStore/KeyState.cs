// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore
{
    public enum KeyState
    {
        /// <summary>
        /// The key was deleted.
        /// </summary>
        /// <remarks>
        /// This value does not signal that the key deleted because it expired.
        /// </remarks>
        Deleted,

        /// <summary>
        /// The key's value changed.
        /// </summary>
        Updated
    }
}
