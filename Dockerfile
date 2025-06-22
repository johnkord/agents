# Multi-stage Dockerfile for building both MCPServer and AgentAlpha
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /src

# Copy solution file and project files for better layer caching
COPY agents.sln .
COPY src/MCPServer/MCPServer.csproj src/MCPServer/
COPY src/Agent/AgentAlpha/AgentAlpha.csproj src/Agent/AgentAlpha/
COPY src/MCPClient/MCPClient.csproj src/MCPClient/
COPY src/Common/Common.csproj src/Common/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ src/

# Build MCPServer
FROM build AS mcpserver-build
RUN dotnet publish src/MCPServer/MCPServer.csproj -c Release -o /app/mcpserver --no-restore

# Build AgentAlpha
FROM build AS agentalpha-build
RUN dotnet publish src/Agent/AgentAlpha/AgentAlpha.csproj -c Release -o /app/agentalpha --no-restore

# Final MCPServer image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS mcpserver
WORKDIR /app
COPY --from=mcpserver-build /app/mcpserver .

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Expose the port
EXPOSE 5000

# Set environment variables for containerized deployment
ENV ASPNETCORE_URLS=http://+:5000
ENV MCP_TRANSPORT=sse
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check endpoint - will need to implement this
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet --list-runtimes || exit 1

ENTRYPOINT ["dotnet", "MCPServer.dll"]

# Final AgentAlpha image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS agentalpha
WORKDIR /app
COPY --from=agentalpha-build /app/agentalpha .

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Set environment variables for containerized deployment
ENV MCP_TRANSPORT=sse
ENV MCP_SERVER_URL=http://mcp-server:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AgentAlpha.dll"]