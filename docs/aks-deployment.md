# AKS Deployment Guide

This guide provides everything needed to deploy the Agents application to an Azure Kubernetes Service (AKS) cluster.

## Overview

The Agents application consists of two main components:
- **MCP Server**: Provides tools and resources via Model Context Protocol
- **AgentAlpha**: AI agent that connects to the MCP Server and uses OpenAI API

Both components are designed to run in containers and communicate via HTTP/SSE transport.

## Prerequisites

### Required Software
- [kubectl](https://kubernetes.io/docs/tasks/tools/) - Kubernetes command-line tool
- [Docker](https://docs.docker.com/get-docker/) - For building container images
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) - For AKS management
- Access to an AKS cluster with appropriate permissions

### Required Credentials
- OpenAI API Key - Required for AgentAlpha to function
- Container registry access - To push/pull Docker images

## Quick Start

### 1. Build and Push Docker Images

First, build the Docker images and push them to a container registry accessible by your AKS cluster.

```bash
# Build MCP Server image
docker build -f src/MCPServer/Dockerfile -t your-registry/mcp-server:latest .
docker push your-registry/mcp-server:latest

# Build AgentAlpha image  
docker build -f src/Agent/AgentAlpha/Dockerfile -t your-registry/agent-alpha:latest .
docker push your-registry/agent-alpha:latest
```

### 2. Update Kubernetes Manifests

Update the image references in the Kubernetes manifests:

```bash
# Update image references
sed -i 's|mcp-server:latest|your-registry/mcp-server:latest|g' k8s/mcp-server.yaml
sed -i 's|agent-alpha:latest|your-registry/agent-alpha:latest|g' k8s/agent-alpha.yaml k8s/agent-alpha-job.yaml
```

### 3. Configure kubectl for AKS

```bash
# Login to Azure
az login

# Get AKS credentials
az aks get-credentials --resource-group your-resource-group --name your-aks-cluster
```

### 4. Deploy to AKS

Run the deployment script:

```bash
./deploy-to-aks.sh
```

The script will:
1. Check cluster connectivity
2. Prompt for your OpenAI API key
3. Deploy all Kubernetes resources
4. Wait for deployments to be ready
5. Show deployment status and logs
6. Optionally run a test job

## Manual Deployment

If you prefer to deploy manually:

```bash
# 1. Create namespace and ConfigMap
kubectl apply -f k8s/namespace-and-config.yaml

# 2. Create secret with your OpenAI API key
kubectl create secret generic agents-secrets \
    --from-literal=OPENAI_API_KEY="your_openai_api_key_here" \
    --namespace=agents

# 3. Deploy MCP Server
kubectl apply -f k8s/mcp-server.yaml

# 4. Wait for MCP Server to be ready
kubectl wait --for=condition=available --timeout=300s deployment/mcp-server -n agents

# 5. Deploy AgentAlpha
kubectl apply -f k8s/agent-alpha.yaml

# 6. Wait for AgentAlpha to be ready
kubectl wait --for=condition=available --timeout=300s deployment/agent-alpha -n agents
```

## Usage Patterns

### Running as Persistent Service

The default deployment runs AgentAlpha as a persistent service in test mode. To customize:

```bash
# Edit the deployment to change the command/args
kubectl edit deployment agent-alpha -n agents

# Or apply a modified version
kubectl apply -f k8s/agent-alpha.yaml
```

### Running One-Time Tasks

Use the Job manifest for one-time tasks:

```bash
# Edit the job to specify your task
vim k8s/agent-alpha-job.yaml

# Run the job
kubectl apply -f k8s/agent-alpha-job.yaml

# Monitor the job
kubectl logs -f job/agent-alpha-task -n agents

# Clean up
kubectl delete job agent-alpha-task -n agents
```

### Running Interactive Tasks

Create a pod for interactive use:

```bash
kubectl run agent-alpha-interactive \
    --image=your-registry/agent-alpha:latest \
    --env="OPENAI_API_KEY=your_key" \
    --env="MCP_TRANSPORT=sse" \
    --env="MCP_SERVER_URL=http://mcp-server:5000" \
    --namespace=agents \
    --rm -it --restart=Never \
    -- dotnet AgentAlpha.dll "Your custom task here"
```

## Configuration

### Environment Variables

Configuration is managed through ConfigMaps and Secrets:

| Variable | Source | Description |
|----------|--------|-------------|
| `OPENAI_API_KEY` | Secret | OpenAI API key (required) |
| `MCP_TRANSPORT` | ConfigMap | Transport mode (sse) |
| `MCP_SERVER_URL` | ConfigMap | MCP Server URL |
| `ASPNETCORE_ENVIRONMENT` | ConfigMap | ASP.NET Core environment |
| `AGENT_MAX_ITERATIONS` | ConfigMap | Max agent iterations |
| `AGENT_TIMEOUT_SECONDS` | ConfigMap | Agent timeout |

### Updating Configuration

```bash
# Update ConfigMap
kubectl edit configmap agents-config -n agents

# Update Secret
kubectl create secret generic agents-secrets \
    --from-literal=OPENAI_API_KEY="new_key" \
    --namespace=agents \
    --dry-run=client -o yaml | kubectl apply -f -

# Restart deployments to pick up changes
kubectl rollout restart deployment/mcp-server -n agents
kubectl rollout restart deployment/agent-alpha -n agents
```

## Monitoring and Troubleshooting

### Check Deployment Status

```bash
./deploy-to-aks.sh --status
```

Or manually:

```bash
kubectl get all -n agents
kubectl get pods -n agents -o wide
```

### View Logs

```bash
./deploy-to-aks.sh --logs
```

Or manually:

```bash
# MCP Server logs
kubectl logs -f deployment/mcp-server -n agents

# AgentAlpha logs
kubectl logs -f deployment/agent-alpha -n agents

# Job logs
kubectl logs job/agent-alpha-task -n agents
```

### Debug Connectivity

```bash
# Test MCP Server connectivity from within cluster
kubectl run debug --image=busybox:1.35 --rm -it --restart=Never -n agents -- sh
# Then inside the container:
nc -zv mcp-server 5000
```

### Common Issues

1. **Image Pull Errors**: Ensure images are pushed to a registry accessible by AKS
2. **OpenAI API Errors**: Verify the API key is correct and has sufficient credits
3. **Network Issues**: Check service discovery and network policies
4. **Resource Limits**: Monitor resource usage and adjust limits if needed

## Scaling

### Horizontal Scaling

```bash
# Scale MCP Server (usually 1 is sufficient)
kubectl scale deployment mcp-server --replicas=2 -n agents

# Scale AgentAlpha (for parallel processing)
kubectl scale deployment agent-alpha --replicas=3 -n agents
```

### Vertical Scaling

Edit resource requests/limits in the deployment manifests:

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "200m"
  limits:
    memory: "1Gi"
    cpu: "1000m"
```

## Security Considerations

- The deployments use non-root users (UID 1000)
- Secrets are used for sensitive data (OpenAI API key)
- Network policies can be added for additional isolation
- RBAC permissions should be scoped appropriately

## Cleanup

```bash
./deploy-to-aks.sh --cleanup
```

Or manually:

```bash
kubectl delete namespace agents
```

## Advanced Configuration

### Custom Resource Quotas

```yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: agents-quota
  namespace: agents
spec:
  hard:
    requests.cpu: "2"
    requests.memory: 2Gi
    limits.cpu: "4"
    limits.memory: 4Gi
    pods: "10"
```

### Network Policies

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: agents-network-policy
  namespace: agents
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: agents
  egress:
  - to: []
    ports:
    - protocol: TCP
      port: 443  # HTTPS for OpenAI API
```

### Health Checks

The deployments include health checks. Customize as needed:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 30
```

## Troubleshooting Guide

### Pod Stuck in Pending

```bash
kubectl describe pod <pod-name> -n agents
# Check for resource constraints or node selectors
```

### Connection Refused Errors

```bash
# Verify service is running
kubectl get svc mcp-server -n agents

# Check endpoints
kubectl get endpoints mcp-server -n agents

# Test connectivity
kubectl run test --image=busybox:1.35 --rm -it --restart=Never -n agents
```

### Agent Not Processing Tasks

1. Check OpenAI API key is valid
2. Verify MCP Server is reachable
3. Check resource limits
4. Review application logs

For more help, check the application logs and Kubernetes events:

```bash
kubectl get events -n agents --sort-by='.firstTimestamp'
```