// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

    public void UpdateDatasetStatus(AssetDatasetEventStreamStatus newStatus)
    {
        Datasets ??= new();

        foreach (AssetDatasetEventStreamStatus currentStatus in Datasets)
        {
            if (currentStatus.Name.Equals(newStatus.Name))
            {
                // If the dataset status is already present in the list, update it in place
                currentStatus.Error = newStatus.Error;
                currentStatus.MessageSchemaReference = newStatus.MessageSchemaReference;
                return;
            }
        }

        // If the dataset status did not exist in the list, just add it
        Datasets.Add(newStatus);
    }

    public void UpdateStreamStatus(AssetDatasetEventStreamStatus newStatus)
    {
        Streams ??= new();

        foreach (AssetDatasetEventStreamStatus currentStatus in Streams)
        {
            if (currentStatus.Name.Equals(newStatus.Name))
            {
                // If the dataset status is already present in the list, update it in place
                currentStatus.Error = newStatus.Error;
                currentStatus.MessageSchemaReference = newStatus.MessageSchemaReference;
                return;
            }
        }

        // If the dataset status did not exist in the list, just add it
        Streams.Add(newStatus);
    }

    public void ClearEventGroupStatus(string eventGroupName)
    {
        if (EventGroups != null)
        {
            AssetEventGroupStatus? eventGroupStatusToRemove = null;
            foreach (AssetEventGroupStatus eventGroupStatus in EventGroups)
            {
                if (eventGroupStatus.Name.Equals(eventGroupName))
                {
                    eventGroupStatusToRemove = eventGroupStatus;
                }
            }

            if (eventGroupStatusToRemove != null)
            {
                EventGroups.Remove(eventGroupStatusToRemove);
            }
        }
    }

    public void UpdateEventStatus(string eventGroupName, AssetDatasetEventStreamStatus eventNewStatus)
    {
        EventGroups ??= new();

        foreach (AssetEventGroupStatus eventGroupStatus in EventGroups)
        {
            if (eventGroupStatus.Name.Equals(eventGroupName))
            {
                eventGroupStatus.Events ??= new();
                foreach (AssetDatasetEventStreamStatus eventStatus in eventGroupStatus.Events)
                {
                    if (eventStatus.Name.Equals(eventNewStatus.Name))
                    {
                        // If the event group status exists and it contains this event's status, update it in place
                        eventStatus.Error = eventNewStatus.Error;
                        eventStatus.MessageSchemaReference = eventNewStatus.MessageSchemaReference;
                        return;
                    }
                }

                // If the event group status exists, but no status was found for this event, just add it.
                eventGroupStatus.Events.Add(eventNewStatus);
                return;
            }
        }

        // If the event group status did not exist, just add it
        EventGroups.Add(new AssetEventGroupStatus()
        {
            Name = eventGroupName,
            Events = new List<AssetDatasetEventStreamStatus> { eventNewStatus }
        });
    }

    public void UpdateManagementGroupStatus(string managementGroupName, AssetManagementGroupActionStatus actionNewStatus)
    {
        ManagementGroups ??= new();

        foreach (AssetManagementGroupStatus managementGroupStatus in ManagementGroups)
        {
            if (managementGroupStatus.Name.Equals(managementGroupName))
            {
                managementGroupStatus.Actions ??= new();
                foreach (AssetManagementGroupActionStatus actionStatus in managementGroupStatus.Actions)
                {
                    if (actionStatus.Name.Equals(actionNewStatus.Name))
                    {
                        // If the management group status exists and it contains this action's status, update it in place
                        actionStatus.Error = actionNewStatus.Error;
                        actionStatus.RequestMessageSchemaReference = actionNewStatus.RequestMessageSchemaReference;
                        actionStatus.ResponseMessageSchemaReference = actionNewStatus.ResponseMessageSchemaReference;
                        return;
                    }
                }

                // If the management group status exists, but no status was found for this action, just add it.
                managementGroupStatus.Actions.Add(actionNewStatus);
                return;
            }
        }

        // If the management group status did not exist, just add it
        ManagementGroups.Add(new AssetManagementGroupStatus()
        {
            Name = managementGroupName,
            Actions = new List<AssetManagementGroupActionStatus> { actionNewStatus }
        });
    }
}
