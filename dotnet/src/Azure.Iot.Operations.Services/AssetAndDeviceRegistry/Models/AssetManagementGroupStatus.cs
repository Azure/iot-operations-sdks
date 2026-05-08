using System.IO;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetManagementGroupStatus
{
    public List<AssetManagementGroupActionStatus>? Actions { get; set; }

    public required string Name { get; set; }

    internal bool EqualTo(AssetManagementGroupStatus other)
    {
        if (!string.Equals(Name, other.Name))
        {
            return false;
        }

        if (Actions == null && other.Actions != null)
        {
            return false;
        }
        else if (Actions != null && other.Actions == null)
        {
            return false;
        }
        else if (Actions != null && other.Actions != null)
        {
            if (Actions.Count != other.Actions.Count)
            {
                return false;
            }

            foreach (var action in Actions)
            {
                // All action entries in this are present exactly once in other
                var matches = other.Actions.Select((a) => a.EqualTo(action));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }

            foreach (var action in other.Actions)
            {
                // All action entries in other are present exactly once in this
                var matches = Actions.Select((a) => a.EqualTo(action));
                if (matches == null || matches.Count() != 1)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
