# Azure IoT Operations (AIO) State Store Command-Line Interface Tool

## Description

Please see [readme.md](./readme.md).

## How to build?

Have rust installed in your local machine.

For a lean release build, run:
```shell
cd ./tools/aiostatestore-cli
cargo build --release --config profile.release.panic=\'abort\'
```

## How to test?

Current requirements:
- Run the test script from the same host of the MQ cluster.
- Setup certificates for TLS and client authentication as done by [update-credentials.sh](../deployment/update-credentials.sh).

Run:
```shell
cd ./tools/aiostatestore-cli/test
./test.sh
```

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
