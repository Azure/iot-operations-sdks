# Kubernetes test resources for ManagementActionConnector

Minimal `Device` + `Asset` pair used to exercise the forthcoming .NET
Management Action API. The asset carries a single management group with
a single action so the connector sample has something concrete to bind
an executor to.

## Files

| File | Purpose |
|---|---|
| `mgmt-action-device-definition.yaml` | Placeholder device (`my-mgmt-action-device`) with one inbound endpoint `my_mgmt_endpoint`. No real backend is reached. |
| `mgmt-action-asset-definition.yaml`  | Asset (`my-mgmt-action-asset`) with management group `device-control` containing one `Call` action named `reboot`. |

## Action identity surfaced by the SDK

Inside `WhileAssetAvailableAsync`, iterating `args.Asset.ManagementGroups`
will yield:

- `group.Name   = "device-control"`
- `action.Name  = "reboot"`
- `action.ActionType = Call`
- `action.Topic = "mgmt/{deviceName}/{assetName}/device-control/reboot"`

The internal SDK key for channels/executors is therefore
`"device-control::reboot"`.

## Apply

```powershell
kubectl apply -f mgmt-action-device-definition.yaml
kubectl apply -f mgmt-action-asset-definition.yaml
```

Adjust the `namespace:` if your Azure IoT Operations install uses
something other than `azure-iot-operations`.

