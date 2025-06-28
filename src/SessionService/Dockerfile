FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

# Install SQLite3 for CLI access (optional)
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/SessionService/SessionService.csproj", "src/SessionService/"]
COPY ["src/Common/Common.csproj", "src/Common/"]

# Restore dependencies
RUN dotnet restore "src/SessionService/SessionService.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/SessionService"
RUN dotnet build "SessionService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SessionService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy static files
COPY --from=build /src/src/SessionService/wwwroot ./wwwroot/

# Create a non-root user and shared data directory
RUN adduser --disabled-password --gecos '' appuser && \
    mkdir -p /app/data && \
    chown -R appuser /app
USER appuser

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENV AGENT_SESSION_DB_PATH=/app/data/agent_sessions.db

ENTRYPOINT ["dotnet", "SessionService.dll"]