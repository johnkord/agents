# Azure Kubernetes Service (AKS) Deployment Guide

This guide provides step-by-step instructions for deploying the Agents MCP system to Azure Kubernetes Service.

> 📘 **For a complete step-by-step deployment guide**, see [Complete AKS Deployment Guide](./aks-complete-deployment-guide.md) which provides detailed commands from start to finish for deploying to an existing AKS cluster.

## Prerequisites

### 1. Azure Resources

- Azure subscription with appropriate permissions
- Azure Kubernetes Service (AKS) cluster
- Azure Container Registry (ACR)
- Domain name for external access (optional)

### 2. Local Tools

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [Helm 3.x](https://helm.sh/docs/intro/install/)
- [Docker](https://docs.docker.com/get-docker/)

### 3. Cluster Requirements

- Kubernetes 1.24+
- Application Gateway Ingress Controller (for ingress)
- Node pools with sufficient resources

## Setup Instructions

### Step 1: Prepare Azure Resources

```bash
# Set variables
RESOURCE_GROUP="rg-agents"
LOCATION="eastus"
AKS_NAME="aks-agents"
ACR_NAME="acragents" # Must be globally unique

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create ACR
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Basic

# Create AKS cluster with ACR integration
az aks create \
    --resource-group $RESOURCE_GROUP \
    --name $AKS_NAME \
    --node-count 2 \
    --node-vm-size Standard_B2s \
    --attach-acr $ACR_NAME \
    --enable-managed-identity \
    --generate-ssh-keys

# Install Application Gateway Ingress Controller (optional)
az aks enable-addons \
    --resource-group $RESOURCE_GROUP \
    --name $AKS_NAME \
    --addons ingress-appgw \
    --appgw-name myApplicationGateway \
    --appgw-subnet-cidr "10.2.0.0/16"
```

### Step 2: Configure Local Environment

```bash
# Get AKS credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME

# Verify connection
kubectl get nodes

# Login to ACR
az acr login --name $ACR_NAME
```

### Step 3: Build and Push Container Images

```bash
# Clone the repository
git clone https://github.com/johnkord/agents.git
cd agents

# Set ACR registry name
export REGISTRY="$ACR_NAME.azurecr.io"

# Run the build script
./scripts/build-and-push.sh $REGISTRY
```

### Step 4: Deploy Applications

#### Option A: Deploy with Helm (Recommended)

```bash
# Deploy with Helm
./scripts/helm-deploy.sh $REGISTRY agents.yourdomain.com your-openai-api-key

# Check deployment status
kubectl get pods -n agents
kubectl get services -n agents
kubectl get ingress -n agents
```

#### Option B: Deploy with Kustomize

```bash
# Update secrets with real values
kubectl create namespace agents

# Create OpenAI API key secret
kubectl create secret generic agents-secrets \
    --from-literal=OPENAI_API_KEY="your-openai-api-key" \
    --from-literal=REST_AUTH_TOKEN="$(openssl rand -base64 32)" \
    --namespace agents

# Deploy with kustomize
./scripts/kustomize-deploy.sh $REGISTRY agents.yourdomain.com
```

### Step 5: Configure DNS and SSL

#### DNS Configuration

```bash
# Get ingress IP
kubectl get ingress agents-ingress -n agents -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

# Add DNS A record pointing your domain to this IP
```

#### SSL Certificate (Optional)

```bash
# Option 1: Use cert-manager for Let's Encrypt
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm install cert-manager jetstack/cert-manager \
    --namespace cert-manager \
    --create-namespace \
    --set installCRDs=true

# Option 2: Upload existing certificate
kubectl create secret tls agents-tls-secret \
    --cert=path/to/cert.crt \
    --key=path/to/cert.key \
    --namespace agents
```

## Verification

### Check Application Status

```bash
# Check all pods are running
kubectl get pods -n agents

# Check services
kubectl get services -n agents

# Check ingress
kubectl get ingress -n agents
```

### Test Applications

```bash
# Get external IP
EXTERNAL_IP=$(kubectl get ingress agents-ingress -n agents -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Test ApprovalService dashboard
curl -k https://$EXTERNAL_IP/

# Test MCP Server
curl -k https://$EXTERNAL_IP/mcp/
```

### View Logs

```bash
# MCPServer logs
kubectl logs -f deployment/agents-mcpserver -n agents

# ApprovalService logs
kubectl logs -f deployment/agents-approval-service -n agents

# Agent job logs
kubectl logs job/agents-agent-alpha-job -n agents
```

## Configuration Options

### Helm Values

Customize deployment by creating a `values-custom.yaml` file:

```yaml
global:
  registry: myregistry.azurecr.io

mcpserver:
  replicaCount: 2
  resources:
    requests:
      memory: "256Mi"
      cpu: "200m"
    limits:
      memory: "512Mi"
      cpu: "500m"

approvalService:
  replicaCount: 1
  persistence:
    enabled: true
    size: 2Gi

agentAlpha:
  type: cronjob
  cronjob:
    schedule: "0 */12 * * *"  # Every 12 hours

ingress:
  enabled: true
  hosts:
    - host: agents.mydomain.com
      paths:
        - path: /
          service:
            name: approval-service
            port: 5000
```

Deploy with custom values:

```bash
helm upgrade --install agents ./helm/agents \
    -f values-custom.yaml \
    --namespace agents \
    --create-namespace
```

### Environment Variables

Key configuration options:

| Variable | Default | Description |
|----------|---------|-------------|
| `APPROVAL_PROVIDER_TYPE` | Rest | Approval mechanism (Console/File/Rest) |
| `MCP_TRANSPORT` | sse | Transport protocol (stdio/sse) |
| `AGENT_MAX_ITERATIONS` | 10 | Maximum agent processing loops |
| `LOGGING__LOGLEVEL__DEFAULT` | Information | Log verbosity level |

## Monitoring and Maintenance

### Resource Monitoring

```bash
# Check resource usage
kubectl top nodes
kubectl top pods -n agents

# Check persistent volumes
kubectl get pvc -n agents
```

### Scaling

```bash
# Scale MCPServer
kubectl scale deployment agents-mcpserver --replicas=3 -n agents

# Scale ApprovalService
kubectl scale deployment agents-approval-service --replicas=2 -n agents
```

### Updates

```bash
# Build new images
./scripts/build-and-push.sh $REGISTRY

# Update Helm deployment
helm upgrade agents ./helm/agents \
    --reuse-values \
    --set images.mcpserver.tag=new-version \
    --namespace agents
```

## Troubleshooting

### Common Issues

1. **Pods stuck in Pending**: Check node resources and storage classes
2. **Image pull errors**: Verify ACR permissions and image names
3. **Ingress not working**: Check Application Gateway configuration
4. **Database connection errors**: Verify persistent volume claims

### Debug Commands

```bash
# Describe problematic pod
kubectl describe pod <pod-name> -n agents

# Check events
kubectl get events -n agents --sort-by=.metadata.creationTimestamp

# Shell into running pod
kubectl exec -it deployment/agents-mcpserver -n agents -- /bin/sh

# Check configuration
kubectl get configmap agents-config -n agents -o yaml
kubectl get secret agents-secrets -n agents -o yaml
```

### Log Collection

```bash
# Collect all logs
kubectl logs -l app.kubernetes.io/name=agents -n agents --all-containers=true

# Stream logs from all pods
kubectl logs -f -l app.kubernetes.io/name=agents -n agents --all-containers=true
```

## Security Considerations

### Pod Security

- All containers run as non-root users
- Security contexts configured with minimal privileges
- Network policies can be added for isolation

### Secrets Management

- Kubernetes secrets for sensitive configuration
- Consider Azure Key Vault integration for production
- Regular rotation of API keys and tokens

### Network Security

- Services use ClusterIP for internal communication
- Ingress provides controlled external access
- Consider service mesh for advanced traffic management

## Cost Optimization

### Resource Management

- Set appropriate resource requests and limits
- Use Horizontal Pod Autoscaler for dynamic scaling
- Consider node pools with different VM sizes

### Storage

- Use appropriate storage classes
- Monitor persistent volume usage
- Implement backup and cleanup policies

### Compute

- Use spot instances for non-critical workloads
- Schedule agent jobs during off-peak hours
- Implement cluster autoscaling

## Production Considerations

### High Availability

- Deploy across multiple availability zones
- Use pod anti-affinity rules
- Implement health checks and readiness probes

### Backup and Recovery

- Regular backup of persistent data
- Document recovery procedures
- Test disaster recovery scenarios

### Monitoring and Alerting

- Implement Prometheus/Grafana for metrics
- Set up alerting for critical issues
- Monitor application performance and errors

## Next Steps

After successful deployment:

1. Configure monitoring and alerting
2. Set up CI/CD pipelines for automated deployments
3. Implement additional security measures
4. Scale according to usage patterns
5. Optimize costs based on actual usage