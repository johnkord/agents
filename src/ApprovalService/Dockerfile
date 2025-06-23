# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/ApprovalService/ApprovalService.csproj", "src/ApprovalService/"]
COPY ["src/MCPServer/MCPServer.csproj", "src/MCPServer/"]
RUN dotnet restore "src/ApprovalService/ApprovalService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/ApprovalService"
RUN dotnet build "ApprovalService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApprovalService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "ApprovalService.dll"]