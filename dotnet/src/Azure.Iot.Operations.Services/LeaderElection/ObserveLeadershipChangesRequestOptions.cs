using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.LeaderElection
{
    public class ObserveLeadershipChangesRequestOptions
    {
        /// <summary>
        /// If true, notifications about this electe changing will include the new holder of the lock after the change.
        /// If false, notifications about this lock changing will not include the new holder.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="LockChangeEventArgs.NewValue"/>
        /// </remarks>
        public bool GetNewLeader { get; set; } = false;

        /// <summary>
        /// If true, notifications about this lock changing will include the previous holder of the lock before the change.
        /// If false, notifications about this lock changing will not include the previous holder.
        /// </summary>
        /// <remarks>
        /// The new value will be set in <see cref="LockChangeEventArgs.PreviousValue"/>
        /// </remarks>
        public bool GetPreviousLeader { get; set; } = false;
    }
}
