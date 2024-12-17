// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.LeasedLock
{
    public class LeasedLockAutomaticRenewalOptions
    {
        /// <summary>
        /// If true, this client will automatically attempt to renew ownership of a lock until this value is set to false or
        /// when the client calls <see cref="LeasedLockClient.ReleaseLockAsync(ReleaseLockRequestOptions?, CancellationToken)"/>
        /// with <see cref="ReleaseLockRequestOptions.CancelAutomaticRenewal"/> set to true.
        /// </summary>
        public bool AutomaticRenewal { get; set; } = false;

        /// <summary>
        /// The period to wait between each attempt to re-acquire the lock.
        /// </summary>
        public TimeSpan RenewalPeriod { get; set; }

        /// <summary>
        /// The length of each lease that this client will automatically re-acquire the lock for.
        /// </summary>
        /// <remarks>
        /// This value only has millisecond-level precision.
        /// </remarks>
        public TimeSpan LeaseTermLength { get; set; }
    }
}
