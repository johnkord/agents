#!/bin/bash

# Deploy Agents to Kubernetes using Kustomize
# Usage: ./scripts/kustomize-deploy.sh <registry> <domain>

set -e

if [ $# -lt 2 ]; then
    echo "Usage: $0 <registry> <domain>"
    echo "Example: $0 myregistry.azurecr.io agents.mydomain.com"
    exit 1
fi

REGISTRY=$1
DOMAIN=$2
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Deploying Agents using Kustomize..."
echo "Registry: $REGISTRY"
echo "Domain: $DOMAIN"

# Create temporary kustomization file with updated values
TEMP_DIR=$(mktemp -d)
cp -r "$ROOT_DIR/k8s/overlays/aks" "$TEMP_DIR/"

# Update registry in kustomization.yaml
sed -i "s|your-registry.azurecr.io|$REGISTRY|g" "$TEMP_DIR/aks/kustomization.yaml"

# Update domain in ingress patch
sed -i "s|agents.yourdomain.com|$DOMAIN|g" "$TEMP_DIR/aks/ingress-patch.yaml"

# Apply the configuration
kubectl apply -k "$TEMP_DIR/aks"

# Clean up
rm -rf "$TEMP_DIR"

echo "Deployment completed successfully!"
echo ""
echo "Note: Update secrets manually with real values:"
echo "  kubectl patch secret agents-secrets -n agents --type='json' -p='[{\"op\": \"replace\", \"path\": \"/data/OPENAI_API_KEY\", \"value\":\"$(echo -n 'your-key' | base64)\"}]'"
echo ""
echo "Services:"
echo "  ApprovalService Dashboard: https://$DOMAIN/"
echo "  MCP Server: https://$DOMAIN/mcp"
echo ""
echo "Check status:"
echo "  kubectl get pods -n agents"
echo "  kubectl get services -n agents"
echo "  kubectl get ingress -n agents"