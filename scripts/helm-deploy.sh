#!/bin/bash

# Deploy Agents to Kubernetes using Helm
# Usage: ./scripts/helm-deploy.sh <registry> <domain> [openai-api-key]

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <registry> <domain> [openai-api-key]"
    echo "Example: $0 myregistry.azurecr.io agents.mydomain.com sk-..."
    exit 1
fi

REGISTRY=$1
DOMAIN=$2
OPENAI_API_KEY=${3:-"your-openai-api-key-here"}
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Deploying Agents to Kubernetes..."
echo "Registry: $REGISTRY"
echo "Domain: $DOMAIN"

# Install/upgrade Helm chart
helm upgrade --install agents "$ROOT_DIR/helm/agents" \
  --set global.registry="$REGISTRY" \
  --set secrets.openaiApiKey="$OPENAI_API_KEY" \
  --set secrets.restAuthToken="$(openssl rand -base64 32)" \
  --set ingress.hosts[0].host="$DOMAIN" \
  --set ingress.hosts[0].paths[0].path="/" \
  --set ingress.hosts[0].paths[0].pathType="Prefix" \
  --set ingress.hosts[0].paths[0].service.name="approval-service" \
  --set ingress.hosts[0].paths[0].service.port=5000 \
  --set ingress.hosts[0].paths[1].path="/mcp" \
  --set ingress.hosts[0].paths[1].pathType="Prefix" \
  --set ingress.hosts[0].paths[1].service.name="mcpserver" \
  --set ingress.hosts[0].paths[1].service.port=3000 \
  --set ingress.tls[0].hosts[0]="$DOMAIN" \
  --set ingress.tls[0].secretName="agents-tls-secret" \
  --namespace agents \
  --create-namespace \
  --wait \
  --timeout=300s

echo "Deployment completed successfully!"
echo ""
echo "Services:"
echo "  ApprovalService Dashboard: https://$DOMAIN/"
echo "  MCP Server: https://$DOMAIN/mcp"
echo ""
echo "Check status:"
echo "  kubectl get pods -n agents"
echo "  kubectl get services -n agents"
echo "  kubectl get ingress -n agents"