// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetStatus
{
    /// <summary>
    /// The status of the asset
    /// </summary>
    /// <remarks>
    /// This status is independent from the status of any nested event groups/datasets/streams. That means that,
    /// even if a dataset has a config error, the asset status may still be okay.
    /// </remarks>
    public ConfigStatus? Config { get; set; }

    /// <summary>
    /// The status of all datasets associated with this asset (if it has any datasets).
    /// </summary>
    /// <remarks>
    /// Each dataset should only report its latest status.
    /// </remarks>
    public List<AssetDatasetEventStreamStatus>? Datasets { get; set; }

    /// <summary>
    /// The status of all event groups associated with this asset (if it has any event groups).
    /// </summary>
    /// <remarks>
    /// Each event group should only report its latest status.
    /// </remarks>
    public List<AssetEventGroupStatus>? EventGroups { get; set; } = default;

    /// <summary>
    /// The status of all management groups associated with this asset (if it has any management groups).
    /// </summary>
    /// <remarks>
    /// Each management group should only report its latest status.
    /// </remarks>
    public List<AssetManagementGroupStatus>? ManagementGroups { get; set; }

    /// <summary>
    /// The status of all streams associated with this asset (if it has any streams).
    /// </summary>
    /// <remarks>
    /// Each stream should only report its latest status.
    /// </remarks>
    public List<AssetDatasetEventStreamStatus>? Streams { get; set; }

    /// <summary>
    /// Update <see cref="Datasets"/> to replace any existing status for the dataset named in <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="newStatus">The new status of the dataset.</param>
    /// <remarks>
    /// If the dataset has no status in <see cref="Datasets"/> yet, <paramref name="newStatus"/> will be added. If the
    /// dataset does have status in <see cref="Datasets"/> already, that status will be replaced entirely by <paramref name="newStatus"/>.
    /// </remarks>
    public void UpdateDatasetStatus(AssetDatasetEventStreamStatus newStatus)
    {
        Datasets ??= new();
        Datasets.RemoveAll((status) => status.Name.Equals(newStatus.Name));
        Datasets.Add(newStatus);
    }

    /// <summary>
    /// Update <see cref="Streams"/> to replace any existing status for the stream named in <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="newStatus">The new status of the stream.</param>
    /// <remarks>
    /// If the stream has no status in <see cref="Streams"/> yet, <paramref name="newStatus"/> will be added. If the
    /// stream does have status in <see cref="Streams"/> already, that status will be replaced entirely by <paramref name="newStatus"/>.
    /// </remarks>
    public void UpdateStreamStatus(AssetDatasetEventStreamStatus newStatus)
    {
        Streams ??= new();

        Streams.RemoveAll((status) => status.Name.Equals(newStatus.Name));
        Streams.Add(newStatus);
    }

    /// <summary>
    /// Remove any statuses related to the provided event group name from <see cref="EventGroups"/>.
    /// </summary>
    /// <param name="eventGroupName">The name of the event group to clear all statuses from.</param>
    public void ClearEventGroupStatus(string eventGroupName)
    {
        EventGroups?.RemoveAll((status) => status.Name.Equals(eventGroupName));
    }

    /// <summary>
    /// Update <see cref="EventGroups"/> to replace any existing status for the provided event group's event's status.
    /// </summary>
    /// <param name="eventGroupName">The name of the event group that this event belongs to.</param>
    /// <param name="eventNewStatus">The new status of the event within this event group.</param>
    public void UpdateEventStatus(string eventGroupName, AssetDatasetEventStreamStatus eventNewStatus)
    {
        EventGroups ??= new();
        bool eventGroupPresent = false;
        EventGroups.ForEach(
            (eventGroupStatus) => {
                if (eventGroupStatus.Name.Equals(eventGroupName))
                {
                    eventGroupPresent = true;
                    eventGroupStatus.Events ??= new();
                    eventGroupStatus.Events.RemoveAll((eventStatus) => eventStatus.Name.Equals(eventNewStatus.Name));
                    eventGroupStatus.Events.Add(eventNewStatus);
                }
            });

        if (!eventGroupPresent)
        {
            EventGroups.Add(new()
            {
                Name = eventGroupName,
                Events = new List<AssetDatasetEventStreamStatus>() { eventNewStatus }
            });
        }
    }

    /// <summary>
    /// Update <see cref="ManagementGroups"/> to replace any existing status for the provided management group action.
    /// </summary>
    /// <param name="managementGroupName">The name of the management group that this action belongs to.</param>
    /// <param name="actionNewStatus">The new status of this action.</param>
    public void UpdateManagementGroupStatus(string managementGroupName, AssetManagementGroupActionStatus actionNewStatus)
    {
        ManagementGroups ??= new();
        ManagementGroups.ForEach(
            (managementGroupStatus) => {
            if (managementGroupStatus.Name.Equals(managementGroupName))
            {
                managementGroupStatus.Actions ??= new();
                managementGroupStatus.Actions.RemoveAll((actionStatus) => actionStatus.Name.Equals(actionNewStatus.Name));
                managementGroupStatus.Actions.Add(actionNewStatus);
            }
        });
    }

    /// <summary>
    /// Compare two asset states.
    /// </summary>
    /// <param name="other">The other asset state to compare against.</param>
    /// <returns>False if there is any difference between the two asset states (ignoring 'LastUpdateTime' field values). True otherwise.</returns>
    /// <remarks>
    /// The 'LastUpdateTime' fields are deliberately ignored in this comparison.
    /// </remarks>
    public bool EqualTo(AssetStatus other)
    {
        if (Config == null && other.Config != null)
        {
            return false;
        }
        else if (Config != null && other.Config == null)
        {
            return false;
        }
        else if (Config != null && other.Config != null && !Config.EqualTo(other.Config))
        {
            return false;
        }

        if (Datasets == null && other.Datasets != null)
        {
            return false;
        }
        else if (Datasets != null && other.Datasets == null)
        {
            return false;
        }
        else if (Datasets != null && other.Datasets != null)
        {
            if (Datasets.Count != other.Datasets.Count)
            {
                return false;
            }

            foreach (var dataset in Datasets)
            {
                // All dataset entries in this are present exactly once in other
                var matches = other.Datasets.Select((a) => a.EqualTo(dataset));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            foreach (var dataset in other.Datasets)
            {
                // All dataset entries in other are present exactly once in this
                var matches = Datasets.Select((a) => a.EqualTo(dataset));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        if (Streams == null && other.Streams != null)
        {
            return false;
        }
        else if (Streams != null && other.Streams == null)
        {
            return false;
        }
        else if (Streams != null && other.Streams != null)
        {
            if (Streams.Count != other.Streams.Count)
            {
                return false;
            }

            foreach (var stream in Streams)
            {
                // All stream entries in this are present exactly once in other
                var matches = other.Streams.Select((a) => a.EqualTo(stream));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            foreach (var stream in other.Streams)
            {
                // All dataset entries in other are present exactly once in this
                var matches = Streams.Select((a) => a.EqualTo(stream));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        if (ManagementGroups == null && other.ManagementGroups != null)
        {
            return false;
        }
        else if (ManagementGroups != null && other.ManagementGroups == null)
        {
            return false;
        }
        else if (ManagementGroups != null && other.ManagementGroups != null)
        {
            if (ManagementGroups.Count != other.ManagementGroups.Count)
            {
                return false;
            }

            foreach (var managementGroup in ManagementGroups)
            {
                // All dataset entries in this are present exactly once in other
                var matches = other.ManagementGroups.Select((a) => a.EqualTo(managementGroup));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            foreach (var managementGroup in other.ManagementGroups)
            {
                // All dataset entries in other are present exactly once in this
                var matches = ManagementGroups.Select((a) => a.EqualTo(managementGroup));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        if (EventGroups == null && other.EventGroups != null)
        {
            return false;
        }
        else if (EventGroups != null && other.EventGroups == null)
        {
            return false;
        }
        else if (EventGroups != null && other.EventGroups != null)
        {
            if (EventGroups.Count != other.EventGroups.Count)
            {
                return false;
            }

            foreach (var eventGroup in EventGroups)
            {
                // All dataset entries in this are present exactly once in other
                var matches = other.EventGroups.Select((a) => a.EqualTo(eventGroup));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            foreach (var eventGroup in other.EventGroups)
            {
                // All dataset entries in other are present exactly once in this
                var matches = EventGroups.Select((a) => a.EqualTo(eventGroup));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
