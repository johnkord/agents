#!/bin/bash

# Deploy Agents to Kubernetes using Helm
# Usage: ./scripts/helm-deploy.sh <registry> [domain] [openai-api-key]
# If domain is not provided or is 'internal', deploys for internal-only access

set -e

if [ $# -lt 1 ]; then
    echo "Usage: $0 <registry> [domain] [openai-api-key]"
    echo "Example: $0 myregistry.azurecr.io agents.mydomain.com sk-..."
    echo "Example (internal): $0 myregistry.azurecr.io internal sk-..."
    exit 1
fi

REGISTRY=$1
DOMAIN=${2:-"internal"}
OPENAI_API_KEY=${3:-"your-openai-api-key-here"}
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Deploying Agents to Kubernetes..."
echo "Registry: $REGISTRY"
echo "Domain: $DOMAIN"

# Check if internal deployment
if [ "$DOMAIN" = "internal" ]; then
    echo "Deploying for internal-only access (no domain required)"
    
    # Install/upgrade Helm chart for internal access
    helm upgrade --install agents "$ROOT_DIR/helm/agents" \
      --set global.registry="$REGISTRY" \
      --set secrets.openaiApiKey="$OPENAI_API_KEY" \
      --set secrets.restAuthToken="$(openssl rand -base64 32)" \
      --set ingress.internalOnly=true \
      --namespace agents \
      --create-namespace \
      --wait \
      --timeout=300s
    
    echo "Deployment completed successfully!"
    echo ""
    echo "Services (Internal Access - Use Port Forwarding):"
    echo "  ApprovalService Dashboard: kubectl port-forward -n agents svc/agents-approval-service 8080:5000"
    echo "  Session Service API: kubectl port-forward -n agents svc/agents-session-service 8081:5001"
    echo "  MCP Server: kubectl port-forward -n agents svc/agents-mcpserver 8082:3000"
    echo ""
    echo "Access URLs after port forwarding:"
    echo "  ApprovalService Dashboard: http://localhost:8080/"
    echo "  Session Service API: http://localhost:8081/api/sessions"
    echo "  MCP Server: http://localhost:8082/"
else
    echo "Deploying for external access with domain: $DOMAIN"
    
    # Install/upgrade Helm chart for external access
    helm upgrade --install agents "$ROOT_DIR/helm/agents" \
      --set global.registry="$REGISTRY" \
      --set secrets.openaiApiKey="$OPENAI_API_KEY" \
      --set secrets.restAuthToken="$(openssl rand -base64 32)" \
      --set ingress.internalOnly=false \
      --set ingress.hosts[0].host="$DOMAIN" \
      --set ingress.hosts[0].paths[0].path="/" \
      --set ingress.hosts[0].paths[0].pathType="Prefix" \
      --set ingress.hosts[0].paths[0].service.name="approval-service" \
      --set ingress.hosts[0].paths[0].service.port=5000 \
      --set ingress.hosts[0].paths[1].path="/api/sessions" \
      --set ingress.hosts[0].paths[1].pathType="Prefix" \
      --set ingress.hosts[0].paths[1].service.name="session-service" \
      --set ingress.hosts[0].paths[1].service.port=5001 \
      --set ingress.hosts[0].paths[2].path="/mcp" \
      --set ingress.hosts[0].paths[2].pathType="Prefix" \
      --set ingress.hosts[0].paths[2].service.name="mcpserver" \
      --set ingress.hosts[0].paths[2].service.port=3000 \
      --set ingress.tls[0].hosts[0]="$DOMAIN" \
      --set ingress.tls[0].secretName="agents-tls-secret" \
      --set ingress.annotations."appgw\.ingress\.kubernetes\.io/ssl-redirect"="true" \
      --set ingress.annotations."appgw\.ingress\.kubernetes\.io/use-private-ip"="false" \
      --namespace agents \
      --create-namespace \
      --wait \
      --timeout=300s
    
    echo "Deployment completed successfully!"
    echo ""
    echo "Services:"
    echo "  ApprovalService Dashboard: https://$DOMAIN/"
    echo "  Session Service API: https://$DOMAIN/api/sessions"
    echo "  MCP Server: https://$DOMAIN/mcp"
fi

echo ""
echo "Check status:"
echo "  kubectl get pods -n agents"
echo "  kubectl get services -n agents"
echo "  kubectl get ingress -n agents"