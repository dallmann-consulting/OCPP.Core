# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080 8443



# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_server
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OCPP.Core.Server/OCPP.Core.Server.csproj", "OCPP.Core.Server/"]
COPY ["OCPP.Core.Database/OCPP.Core.Database.csproj", "OCPP.Core.Database/"]
COPY ["OCPP.Core.Server.Extensions/OCPP.Core.Server.Extensions.csproj", "OCPP.Core.Server.Extensions/"]
RUN dotnet restore "./OCPP.Core.Server/OCPP.Core.Server.csproj"
COPY . .
WORKDIR "/src/OCPP.Core.Server"
RUN dotnet build "./OCPP.Core.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build_server AS publish_server
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OCPP.Core.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final_server
WORKDIR /app
COPY --chown=$APP_UID --from=publish_server /app/publish .
ENTRYPOINT ["dotnet", "OCPP.Core.Server.dll"]
RUN mkdir /tmp/ocpp

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build_management
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OCPP.Core.Management/OCPP.Core.Management.csproj", "OCPP.Core.Management/"]
COPY ["OCPP.Core.Database/OCPP.Core.Database.csproj", "OCPP.Core.Database/"]
RUN dotnet restore "./OCPP.Core.Management/OCPP.Core.Management.csproj"
COPY . .
WORKDIR "/src/OCPP.Core.Management"
RUN dotnet build "./OCPP.Core.Management.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build_management AS publish_management
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OCPP.Core.Management.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final_management
WORKDIR /app
COPY --chown=$APP_UID --from=publish_management /app/publish .
ENTRYPOINT ["dotnet", "OCPP.Core.Management.dll"]
