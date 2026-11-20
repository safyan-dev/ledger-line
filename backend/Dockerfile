# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Api/Api.csproj

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Api/Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# One-shot migration runner. Production never migrates on startup, so CD runs
# this image exactly once before the application rollout.
# Restore deliberately keeps the NuGet cache in the layer: `dotnet ef` resolves
# the EF Core Design assembly at runtime.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS migrate
WORKDIR /src

RUN dotnet tool install --global dotnet-ef --version 10.0.4
ENV PATH="$PATH:/root/.dotnet/tools" \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

RUN dotnet build src/Api/Api.csproj --configuration Release

ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--no-build", \
    "--configuration", "Release", \
    "--project", "src/Persistence/Persistence.csproj", \
    "--startup-project", "src/Api/Api.csproj"]

# Default target, so `docker build` without --target produces the application.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=build /app/publish .

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Api.dll"]
