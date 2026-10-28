# Base dotnet image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt upgrade -y && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .csharpierrc .csharpierrc

RUN dotnet tool restore

COPY src/TradeImportsFisSimulatorApi/TradeImportsFisSimulatorApi.csproj src/TradeImportsFisSimulatorApi/TradeImportsFisSimulatorApi.csproj
COPY tests/TradeImportsFisSimulatorApi.Test/TradeImportsFisSimulatorApi.Test.csproj tests/TradeImportsFisSimulatorApi.Test/TradeImportsFisSimulatorApi.Test.csproj
COPY tests/TradeImportsFisSimulatorApi.IntegrationTests/TradeImportsFisSimulatorApi.IntegrationTests.csproj tests/TradeImportsFisSimulatorApi.IntegrationTests/TradeImportsFisSimulatorApi.IntegrationTests.csproj
COPY TradeImportsFisSimulatorApi.sln TradeImportsFisSimulatorApi.sln

COPY src/TradeImportsFisSimulatorApi src/TradeImportsFisSimulatorApi
COPY tests/TradeImportsFisSimulatorApi.Test tests/TradeImportsFisSimulatorApi.Test
COPY tests/TradeImportsFisSimulatorApi.IntegrationTests tests/TradeImportsFisSimulatorApi.IntegrationTests

RUN dotnet csharpier check .

COPY NuGet.config NuGet.config
ARG DEFRA_NUGET_PAT

RUN dotnet restore

RUN dotnet build src/TradeImportsFisSimulatorApi/TradeImportsFisSimulatorApi.csproj --no-restore -c Release

RUN dotnet test --no-restore --filter "Category!=IntegrationTest"

FROM build AS publish

RUN dotnet publish src/TradeImportsFisSimulatorApi -c Release -o /app/publish /p:UseAppHost=false

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8085
USER app
ENTRYPOINT ["dotnet", "TradeImportsFisSimulatorApi.dll"]
