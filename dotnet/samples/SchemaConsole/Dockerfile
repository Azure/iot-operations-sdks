# Use the .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Copy the nuget.config file for private feed access
COPY nuget.config ./nuget.config

# Copy project file and restore dependencies using the private feed
COPY SchemaConsole.csproj ./
RUN dotnet restore "./SchemaConsole.csproj" --configfile ./nuget.config

# Copy the rest of the source code and publish
COPY . ./
RUN dotnet publish -c Release -o /app/out

# Use the .NET Runtime image for the final container
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "SchemaConsole.dll"]
