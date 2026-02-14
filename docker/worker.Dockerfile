# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Aonik.sln ./
COPY src/Aonik.Worker/Aonik.Worker.csproj src/Aonik.Worker/
COPY src/Aonik.Application/Aonik.Application.csproj src/Aonik.Application/
COPY src/Aonik.Domain/Aonik.Domain.csproj src/Aonik.Domain/
COPY src/Aonik.Infrastructure/Aonik.Infrastructure.csproj src/Aonik.Infrastructure/
COPY src/Aonik.SharedKernel/Aonik.SharedKernel.csproj src/Aonik.SharedKernel/

RUN dotnet restore src/Aonik.Worker/Aonik.Worker.csproj

FROM restore AS publish
COPY src ./src
RUN dotnet publish src/Aonik.Worker/Aonik.Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Aonik.Worker.dll"]
