FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

# Install SQLite3 for CLI access (optional)
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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

# Create data directory with proper permissions
RUN mkdir -p /app/data && chown -R app:app /app/data

# Create app user
RUN groupadd -r app && useradd -r -g app app

# Copy published app
COPY --from=publish /app/publish .

# Copy static files
COPY --from=build /src/src/SessionService/wwwroot ./wwwroot/

# Set ownership
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENV AGENT_SESSION_DB_PATH=/app/data/agent_sessions.db

ENTRYPOINT ["dotnet", "SessionService.dll"]