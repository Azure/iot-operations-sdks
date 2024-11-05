# Akri Connector Configuration

In production, the Akri connector is configure by the Akri Operator, and the ADR client is used to read the configuration into the application.

## MQTT broker

1. Set the environment variables:

    | Name | Description |
    |-|-|
    | CONFIGMAP_MOUNT_PATH | |

1. Create the following files in the `CONFIGMAP_MOUNT_PATH` directory:

    | Name | Contents |
    |-|-|
    | MQ_TARGET_ADDRESS | The MQTT broker hostname |
    | MQ_TARGET_PORT | The MQTT broker port |
    | MQ_USE_TLS | Enable TLS on the MQTT broker connection |
    | MQ_SAT_MOUNT_PATH | The Service Account Token |
    | MQ_TLS_CACERT_MOUNT_PATH | The MQTT Broker CA cert |

## Asset endpoint

1. Set the following environment variables:

    | Name | Description |
    |-|-|
    | AEP_MQ_CONFIGMAP_MOUNT_PATH | The config map volume mount path for AEP |
    | AEP_USERNAME_SECRET_MOUNT_PATH | |
    | AEP_PASSWORD_SECRET_MOUNT_PATH | |
    | AEP_CERT_MOUNT_PATH | |

1. Create the following files:

    | Name | Description |
    |-|-|
    | AEP_TARGET_ADDRESS | The hostname of the asset endpoint |
    | AEP_AUTHENTICATION_METHOD | The authentication method |
    | AEP_USERNAME_FILE_NAME | The file containing the username |
    | AEP_PASSWORD_FILE_NAME | The file containing the password|
    | AEP_CERT_FILE_NAME | The file containing the server certificate |
    | ENDPOINT_PROFILE_TYPE | The type of endpoint |
    | AEP_ADDITIONAL_CONFIGURATION | Any additional configuration required by the connector |
    | AEP_DISCOVERED_ASSET_ENDPOINT_PROFILE_REF | |
    | AEP_UUID | |

## Assets

1. Set the following environment variables:

    | Name | Description |
    |-|-|
    | ASSET_CONFIGMAP_MOUNT_PATH | |\

1. Create the following files  in the `ASSET_CONFIGMAP_MOUNT_PATH` directory:

    | Name | Description |
    |-|-|
    | <assetName>/<assetName> | Json file containing the asset definition |


1. Create the asset definition

[TODO] This is huge

    | Key | Value |
    |-|-|
    | | |
