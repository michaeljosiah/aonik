# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Aonik.sln ./
COPY src/Aonik.Api/Aonik.Api.csproj src/Aonik.Api/
COPY src/Aonik.Application/Aonik.Application.csproj src/Aonik.Application/
COPY src/Aonik.Domain/Aonik.Domain.csproj src/Aonik.Domain/
COPY src/Aonik.Infrastructure/Aonik.Infrastructure.csproj src/Aonik.Infrastructure/
COPY src/Aonik.SharedKernel/Aonik.SharedKernel.csproj src/Aonik.SharedKernel/

RUN dotnet restore src/Aonik.Api/Aonik.Api.csproj

FROM restore AS publish
COPY src ./src
RUN dotnet publish src/Aonik.Api/Aonik.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

USER ${APP_UID}

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Aonik.Api.dll"]
