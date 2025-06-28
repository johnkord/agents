# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/Agent/AgentAlpha/AgentAlpha.csproj", "src/Agent/AgentAlpha/"]
COPY ["src/Common/Common.csproj", "src/Common/"]
COPY ["src/MCPClient/MCPClient.csproj", "src/MCPClient/"]
RUN dotnet restore "src/Agent/AgentAlpha/AgentAlpha.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Agent/AgentAlpha"
RUN dotnet build "AgentAlpha.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AgentAlpha.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a non-root user and shared data directory
RUN adduser --disabled-password --gecos '' appuser && \
    mkdir -p /app/data && \
    chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "AgentAlpha.dll"]