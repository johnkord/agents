#!/bin/bash

# Build and Push Container Images to Azure Container Registry
# Usage: ./scripts/build-and-push.sh <registry-name>

set -e

if [ $# -eq 0 ]; then
    echo "Usage: $0 <registry-name>"
    echo "Example: $0 myregistry.azurecr.io"
    exit 1
fi

REGISTRY=$1
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Building and pushing images to $REGISTRY"

# Login to ACR (assumes Azure CLI is configured)
echo "Logging into ACR..."
az acr login --name ${REGISTRY%%.*}

# Build and push MCPServer
echo "Building MCPServer..."
docker build -f "$ROOT_DIR/src/MCPServer/Dockerfile" -t "$REGISTRY/mcpserver:latest" "$ROOT_DIR"
echo "Pushing MCPServer..."
docker push "$REGISTRY/mcpserver:latest"

# Build and push ApprovalService
echo "Building ApprovalService..."
docker build -f "$ROOT_DIR/src/ApprovalService/Dockerfile" -t "$REGISTRY/approval-service:latest" "$ROOT_DIR"
echo "Pushing ApprovalService..."
docker push "$REGISTRY/approval-service:latest"

# Build and push AgentAlpha
echo "Building AgentAlpha..."
docker build -f "$ROOT_DIR/src/Agent/AgentAlpha/Dockerfile" -t "$REGISTRY/agent-alpha:latest" "$ROOT_DIR"
echo "Pushing AgentAlpha..."
docker push "$REGISTRY/agent-alpha:latest"

# Build and push SessionService
echo "Building SessionService..."
docker build -f "$ROOT_DIR/src/SessionService/Dockerfile" -t "$REGISTRY/session-service:latest" "$ROOT_DIR"
echo "Pushing SessionService..."
docker push "$REGISTRY/session-service:latest"

# Build and push MCPClient (optional)
echo "Building MCPClient..."
docker build -f "$ROOT_DIR/src/MCPClient/Dockerfile" -t "$REGISTRY/mcpclient:latest" "$ROOT_DIR"
echo "Pushing MCPClient..."
docker push "$REGISTRY/mcpclient:latest"

echo "All images built and pushed successfully!"
echo ""
echo "Images available:"
echo "  $REGISTRY/mcpserver:latest"
echo "  $REGISTRY/approval-service:latest"
echo "  $REGISTRY/agent-alpha:latest"
echo "  $REGISTRY/session-service:latest"
echo "  $REGISTRY/mcpclient:latest"