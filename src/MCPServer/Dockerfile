# Use the official .NET 8.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /build

# Copy the solution file and project files for layer caching
COPY agents.sln .

# Copy all project files
COPY src/MCPServer/MCPServer.csproj src/MCPServer/
COPY src/Common/Common.csproj src/Common/
COPY src/MCPClient/MCPClient.csproj src/MCPClient/
COPY src/Agent/AgentAlpha/AgentAlpha.csproj src/Agent/AgentAlpha/

# Restore only the MCPServer project and its dependencies
RUN dotnet restore src/MCPServer/MCPServer.csproj

# Copy source code
COPY src/ src/

# Build and publish MCPServer
RUN dotnet publish src/MCPServer/MCPServer.csproj -c Release -o /app/publish --no-restore

# Use the runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set working directory
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Expose the port
EXPOSE 5000

# Set environment variables for containerized deployment
ENV ASPNETCORE_URLS=http://+:5000
ENV MCP_TRANSPORT=sse
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check (basic runtime check)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet --list-runtimes || exit 1

# Start the application
ENTRYPOINT ["dotnet", "MCPServer.dll"]