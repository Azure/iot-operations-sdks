// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class LeaderElectionAutomaticRenewalOptions
    {
        /// <summary>
        /// If true, this client will automatically run for re-election until this value is set to false or
        /// when the client calls <see cref="LeaderElectionClient.ResignAsync(ResignationRequestOptions?, CancellationToken)"/>
        /// with <see cref="ResignationRequestOptions.CancelAutomaticRenewal"/> set to true.
        /// </summary>
        public bool AutomaticRenewal { get; set; } = false;

        /// <summary>
        /// The period to wait between each attempt to campaign to be leader.
        /// </summary>
        public TimeSpan RenewalPeriod { get; set; }

        /// <summary>
        /// The length of each term that this client will automatically campaign for.
        /// </summary>
        /// <remarks>
        /// This value only has millisecond-level precision.
        /// </remarks>
        public TimeSpan ElectionTerm { get; set; }
    }
}
