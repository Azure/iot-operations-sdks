// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.StateStore
{
    public enum SetCondition
    {
        /// <summary>
        /// The set operation will only execute if the State Store does not have this key already.
        /// </summary>
        OnlyIfNotSet,

        /// <summary>
        /// The set operation will only execute if the State Store does not have this key or it has this key and
        /// the value in the State Store is equal to the value provided for this set operation.
        /// </summary>
        OnlyIfEqualOrNotSet,

        /// <summary>
        /// The set operation will execute regardless of if the key exists already and regardless of the value
        /// of this key in the State Store.
        /// </summary>
        Unconditional,
    }
}