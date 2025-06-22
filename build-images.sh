#!/bin/bash

# Build Docker images for Agents
# This script builds both MCP Server and AgentAlpha Docker images

set -e

# Configuration
REGISTRY=${REGISTRY:-"localhost"}
TAG=${TAG:-"latest"}
PUSH=${PUSH:-"false"}

echo "🐳 Building Docker images for Agents"
echo "===================================="
echo "Registry: $REGISTRY"
echo "Tag: $TAG"
echo "Push: $PUSH"
echo ""

# Function to build an image
build_image() {
    local component=$1
    local dockerfile=$2
    local image_name="$REGISTRY/$component:$TAG"
    
    echo "🏗️  Building $component..."
    docker build -f "$dockerfile" -t "$image_name" .
    echo "✅ Built $image_name"
    
    if [ "$PUSH" = "true" ]; then
        echo "📤 Pushing $image_name..."
        docker push "$image_name"
        echo "✅ Pushed $image_name"
    fi
    echo ""
}

# Build MCP Server
build_image "mcp-server" "src/MCPServer/Dockerfile"

# Build AgentAlpha
build_image "agent-alpha" "src/Agent/AgentAlpha/Dockerfile"

echo "🎉 All images built successfully!"

if [ "$PUSH" = "false" ]; then
    echo ""
    echo "💡 To push images to registry, run:"
    echo "   PUSH=true $0"
    echo ""
    echo "💡 To use a different registry, run:"
    echo "   REGISTRY=your-registry.com PUSH=true $0"
fi

echo ""
echo "📋 Built images:"
docker images | grep -E "(mcp-server|agent-alpha)" | grep "$TAG"

echo ""
echo "📖 Next steps:"
echo "  1. Update image references in k8s/*.yaml files:"
echo "     sed -i 's|mcp-server:latest|$REGISTRY/mcp-server:$TAG|g' k8s/mcp-server.yaml"
echo "     sed -i 's|agent-alpha:latest|$REGISTRY/agent-alpha:$TAG|g' k8s/agent-alpha.yaml k8s/agent-alpha-job.yaml"
echo "  2. Deploy to AKS: ./deploy-to-aks.sh"