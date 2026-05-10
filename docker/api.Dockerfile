# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Aonik.sln ./
COPY src/Aonik.Api/Aonik.Api.csproj src/Aonik.Api/
COPY src/Aonik.Application/Aonik.Application.csproj src/Aonik.Application/
COPY src/Aonik.Infrastructure/Aonik.Infrastructure.csproj src/Aonik.Infrastructure/
COPY src/Aonik.SharedKernel/Aonik.SharedKernel.csproj src/Aonik.SharedKernel/
COPY src/Aonik.Platform/Aonik.Platform.csproj src/Aonik.Platform/
COPY src/Aonik.Finance/Aonik.Finance.csproj src/Aonik.Finance/
COPY src/Aonik.Ai/Aonik.Ai.csproj src/Aonik.Ai/
COPY src/Aonik.Agents/Aonik.Agents.csproj src/Aonik.Agents/
COPY src/Aonik.ServiceDefaults/Aonik.ServiceDefaults.csproj src/Aonik.ServiceDefaults/
COPY src/Aonik.Voice/Aonik.Voice.csproj src/Aonik.Voice/
COPY src/Aonik.Worker/Aonik.Worker.csproj src/Aonik.Worker/
COPY src/Aonik.Migrator/Aonik.Migrator.csproj src/Aonik.Migrator/
COPY src/Aonik.Platform.Mcp/Aonik.Platform.Mcp.csproj src/Aonik.Platform.Mcp/
COPY src/Aonik.Finance.Mcp/Aonik.Finance.Mcp.csproj src/Aonik.Finance.Mcp/
COPY src/Aonik.AppHost/Aonik.AppHost.csproj src/Aonik.AppHost/

RUN dotnet restore src/Aonik.Api/Aonik.Api.csproj

FROM restore AS publish
COPY src ./src
RUN dotnet publish src/Aonik.Api/Aonik.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ARG APP_UID=1654

COPY --from=publish /app/publish .

# Local blob storage defaults to /app/App_Data via the relative App_Data
# base path. Create a writable directory before dropping privileges so
# startup storage probes and local uploads do not fail under the app user.
RUN mkdir -p /app/App_Data && chown -R ${APP_UID}:0 /app/App_Data

USER ${APP_UID}

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Aonik.Api.dll"]
