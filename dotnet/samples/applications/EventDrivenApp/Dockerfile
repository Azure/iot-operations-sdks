# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Build application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build
COPY . .
RUN dotnet publish -o dist

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /build/dist .
ENTRYPOINT ["./EventDrivenApp"]
