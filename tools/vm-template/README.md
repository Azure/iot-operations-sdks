## Build the arm definition

```bash
az bicep build -f deployVm.bicep
```

## Deploy the template

1. Go [here](https://portal.azure.com/#create/Microsoft.Template)

1. Select "Build your own template in the editor"

1. Paste in the contents of deployVm.json


## Debug

1. Cloud init output:

    ```shell
    cat /var/log/cloud-init-output.log
    cat /var/log/cloud-init.log
    ```

1. Cloud init status:

1. Custom extension:

    ```shell
    /var/lib/waagent/custom-script/download/0
    ```
