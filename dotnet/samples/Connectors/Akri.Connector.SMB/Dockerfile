FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY connectors/Akri.Connector.SMB/Akri.Connector.SMB.csproj connectors/Akri.Connector.SMB/
COPY src/Akri.HistorianConnector.Core/Akri.HistorianConnector.Core.csproj src/Akri.HistorianConnector.Core/
COPY src/Akri.ConnectorFramework.Authentication/Akri.ConnectorFramework.Authentication.csproj src/Akri.ConnectorFramework.Authentication/
COPY src/ResilientQueryRunner/ResilientQueryRunner.csproj src/ResilientQueryRunner/
COPY Directory.Build.props Directory.Packages.props global.json NuGet.config ./

# Restore dependencies
RUN dotnet restore connectors/Akri.Connector.SMB/Akri.Connector.SMB.csproj

# Copy all source files
COPY connectors/Akri.Connector.SMB/ connectors/Akri.Connector.SMB/
COPY src/ src/

# Build and publish
RUN dotnet publish connectors/Akri.Connector.SMB/Akri.Connector.SMB.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "Akri.Connector.SMB.dll"]
