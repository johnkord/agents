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

# Restore only the AgentAlpha project and its dependencies
RUN dotnet restore src/Agent/AgentAlpha/AgentAlpha.csproj

# Copy source code
COPY src/ src/

# Build and publish AgentAlpha
RUN dotnet publish src/Agent/AgentAlpha/AgentAlpha.csproj -c Release -o /app/publish --no-restore

# Use the runtime image for the final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime

# Set working directory
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Set environment variables for containerized deployment
ENV MCP_TRANSPORT=sse
ENV MCP_SERVER_URL=http://mcp-server:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Start the application
ENTRYPOINT ["dotnet", "AgentAlpha.dll"]