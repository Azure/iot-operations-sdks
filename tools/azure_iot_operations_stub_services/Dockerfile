FROM mcr.microsoft.com/azurelinux/base/core:3.0

RUN mkdir -p /opt/app/data
COPY target/release/azure_iot_operations_stub_services/ /opt/app
RUN chown -R 65532:65532 /opt/app

ENV RUST_BACKTRACE=1 STUB_SERVICE_OUTPUT_DIR="/opt/app/data" AIO_BROKER_HOSTNAME="aio-broker" AIO_BROKER_TCP_PORT="1883"

USER 65532
ENTRYPOINT /opt/app/azure_iot_operations_stub_services --broker-addr $AIO_BROKER_HOSTNAME --broker-port $AIO_BROKER_TCP_PORT

