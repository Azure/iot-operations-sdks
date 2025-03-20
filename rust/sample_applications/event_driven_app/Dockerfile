# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

FROM alpine:3
WORKDIR /app
COPY input_client/out/debug/input_client .
COPY output_client/out/debug/output_client .
ENTRYPOINT ["./input_client"]