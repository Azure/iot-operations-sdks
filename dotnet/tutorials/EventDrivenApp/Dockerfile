# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY bin/Release/net9.0/linux-x64/publish/ .
ENTRYPOINT ["./EventDrivenApp"]