// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    public record AssetEventGroupStatus
    {
        /// <summary>
        /// Array of event statuses that describe the status of each event in the event group.
        /// </summary>
        public List<AssetDatasetEventStreamStatus>? Events { get; set; } = default;

        /// <summary>
        /// The name of the event group. Must be unique within the status.eventGroups array. This name is used to correlate between the spec and status event group information.
        /// </summary>
        public string Name { get; set; } = default!;

        internal bool EqualTo(AssetEventGroupStatus other)
        {
            if (!string.Equals(Name, other.Name))
            {
                return false;
            }

            if (Events == null && other.Events != null)
            {
                return false;
            }
            else if (Events != null && other.Events == null)
            {
                return false;
            }
            else if (Events != null && other.Events != null)
            {
                if (Events.Count != other.Events.Count)
                {
                    return false;
                }

                foreach (var assetEvent in Events)
                {
                    // All event entries in this are present exactly once in other
                    var matches = other.Events.Select((a) => a.EqualTo(assetEvent));
                    if (matches == null || matches.Count() != 1)
                    {
                        return false;
                    }
                }

                foreach (var assetEvent in other.Events)
                {
                    // All event entries in other are present exactly once in this
                    var matches = Events.Select((a) => a.EqualTo(assetEvent));
                    if (matches == null || matches.Count() != 1)
                    {
                        return false;
                    }
                }
            }


            return true;
        }
    }
}
