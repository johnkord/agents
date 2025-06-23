# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/MCPClient/MCPClient.csproj", "src/MCPClient/"]
RUN dotnet restore "src/MCPClient/MCPClient.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/MCPClient"
RUN dotnet build "MCPClient.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MCPClient.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "MCPClient.dll"]