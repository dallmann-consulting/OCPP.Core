# Build OCPP Server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build_server
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OCPP.Core.Server/OCPP.Core.Server.csproj", "OCPP.Core.Server/"]
COPY ["OCPP.Core.Database/OCPP.Core.Database.csproj", "OCPP.Core.Database/"]
COPY ["OCPP.Core.Server.Extensions/OCPP.Core.Server.Extensions.csproj", "OCPP.Core.Server.Extensions/"]
RUN dotnet restore "./OCPP.Core.Server/OCPP.Core.Server.csproj"
COPY . .
RUN dotnet publish "./OCPP.Core.Server/OCPP.Core.Server.csproj" -c $BUILD_CONFIGURATION -o /app/server /p:UseAppHost=false

# Build Management UI
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build_management
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OCPP.Core.Management/OCPP.Core.Management.csproj", "OCPP.Core.Management/"]
COPY ["OCPP.Core.Database/OCPP.Core.Database.csproj", "OCPP.Core.Database/"]
RUN dotnet restore "./OCPP.Core.Management/OCPP.Core.Management.csproj"
COPY . .
RUN dotnet publish "./OCPP.Core.Management/OCPP.Core.Management.csproj" -c $BUILD_CONFIGURATION -o /app/management /p:UseAppHost=false

# Final single-container image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
LABEL org.opencontainers.image.source="https://github.com/dallmann-consulting/OCPP.Core"
WORKDIR /app
EXPOSE 8081 8082

# Defaults — override via environment variables or compose.yaml
ENV ASPNETCORE_ENVIRONMENT="Production" \
    ConnectionStrings__SQLite="Filename=/db/OCPP.Core.sqlite;" \
    MessageDumpDir="/tmp/ocpp" \
    ServerApiUrl="http://localhost:8081/API" \
    ApiKey="" \
    Logging__LogLevel__Default="Information" \
    Logging__LogLevel__OCPP="Information" \
    Users__0__Username="admin" \
    Users__0__Password="t3st" \
    Users__0__Administrator="true"

COPY --chown=app:app --from=build_server /app/server ./server/
COPY --chown=app:app --from=build_management /app/management ./management/
COPY docker-start.sh /start.sh

RUN mkdir -p /db /tmp/ocpp && \
    chown app:app /db /tmp/ocpp && \
    sed -i 's/\r//' /start.sh && \
    chmod +x /start.sh

USER app
ENTRYPOINT ["/start.sh"]
