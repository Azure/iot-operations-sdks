# How to package connector metadata for connector images

In order for the user of a connector to access the metadata associated with it, the publisher of a connector must push the metadata using ORAS like:

```bash
oras push --config <path_to_empty_file>:application/vnd.microsoft.akri-connector.v1+json <container_registry>/<connector_name>-metadata:<connector_version> <connector_metadata_file>
```

where: 
 - <path_to_empty_file> can be any file ("/dev/null" is a good choice for linux) as the contents won't be checked 
 - The connector metadata file is a JSON file that adheres to the schema defined [here](https://raw.githubusercontent.com/SchemaStore/schemastore/refs/heads/master/src/schemas/json/aio-connector-metadata-9.0-preview.json) such as the examples in this folder.


for example:

```bash
oras push --config /dev/null:application/vnd.microsoft.akri-connector.v1+json someAcr.azurecr.io/akri-connectors/minimal-example-connector-metadata:1.0.0 ./minimal-example-connector-metadata.json
```

See [this doc](https://oras.land/docs/commands/oras_push/) for more options/details when using ORAS to push.
