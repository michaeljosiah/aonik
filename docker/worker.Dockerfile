# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Aonik.sln ./
COPY src/Aonik.Worker/Aonik.Worker.csproj src/Aonik.Worker/
COPY src/Aonik.Application/Aonik.Application.csproj src/Aonik.Application/
COPY src/Aonik.Infrastructure/Aonik.Infrastructure.csproj src/Aonik.Infrastructure/
COPY src/Aonik.SharedKernel/Aonik.SharedKernel.csproj src/Aonik.SharedKernel/
COPY src/Aonik.Platform/Aonik.Platform.csproj src/Aonik.Platform/
COPY src/Aonik.Finance/Aonik.Finance.csproj src/Aonik.Finance/
COPY src/Aonik.Ai/Aonik.Ai.csproj src/Aonik.Ai/
COPY src/Aonik.Agents/Aonik.Agents.csproj src/Aonik.Agents/
COPY src/Aonik.ServiceDefaults/Aonik.ServiceDefaults.csproj src/Aonik.ServiceDefaults/
COPY src/Aonik.Api/Aonik.Api.csproj src/Aonik.Api/
COPY src/Aonik.Migrator/Aonik.Migrator.csproj src/Aonik.Migrator/
COPY src/Aonik.Platform.Mcp/Aonik.Platform.Mcp.csproj src/Aonik.Platform.Mcp/
COPY src/Aonik.Finance.Mcp/Aonik.Finance.Mcp.csproj src/Aonik.Finance.Mcp/
COPY src/Aonik.AppHost/Aonik.AppHost.csproj src/Aonik.AppHost/

RUN dotnet restore src/Aonik.Worker/Aonik.Worker.csproj

FROM restore AS publish
COPY src ./src
RUN dotnet publish src/Aonik.Worker/Aonik.Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ARG APP_UID=1654
USER ${APP_UID}

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Aonik.Worker.dll"]
