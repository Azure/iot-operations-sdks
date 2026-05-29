# Kubernetes test resources for ManagementActionConnector

Minimal `Device` + `Asset` pair used to exercise the .NET Management
Action API. The asset carries a single management group with three
actions (one of each `ActionType`) so the connector sample has something
concrete to bind executors to.

## Files

| File | Purpose |
|---|---|
| `mgmt-action-device-definition.yaml` | Placeholder device (`my-mgmt-action-device`) with one inbound endpoint `my_mgmt_endpoint`. No real backend is reached. |
| `mgmt-action-asset-definition.yaml`  | Asset (`my-mgmt-action-asset`) with management group `device-control` containing three actions: `reboot` (Call), `read-temperature` (Read), and `write-configuration` (Write). |

## Action identity surfaced by the SDK

Inside `WhileAssetAvailableAsync`, iterating `args.Asset.ManagementGroups`
will yield group `device-control` with three actions:

| `action.Name` | `action.ActionType` | `action.Topic` | Internal key |
|---|---|---|---|
| `reboot` | `Call` | `mgmt/device-1/asset-1/device-control/reboot` | `device-control::reboot` |
| `read-temperature` | `Read` | `mgmt/device-1/asset-1/device-control/read-temperature` | `device-control::read-temperature` |
| `write-configuration` | `Write` | `mgmt/device-1/asset-1/device-control/write-configuration` | `device-control::write-configuration` |

## Apply

```powershell
kubectl apply -f mgmt-action-device-definition.yaml
kubectl apply -f mgmt-action-asset-definition.yaml
```

Adjust the `namespace:` if your Azure IoT Operations install uses
something other than `azure-iot-operations`.

