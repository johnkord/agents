# Kubernetes Deployment for Agents

This directory contains Kubernetes manifests and Helm charts for deploying the Agents MCP system on Azure Kubernetes Service (AKS).

## Architecture

The deployment consists of:

- **MCPServer**: The core MCP server providing mathematical tools and system utilities
- **ApprovalService**: Web-based tool approval service with dashboard  
- **SessionService**: HTTP API service for managing agent sessions and conversation state
- **AgentAlpha**: AI agent that can run as a Job or CronJob
- **Supporting Infrastructure**: ConfigMaps, Secrets, Services, Ingress, Shared Persistent Storage

## Prerequisites

### AKS Cluster
- Azure Kubernetes Service cluster with Application Gateway Ingress Controller
- kubectl configured to access your cluster
- Helm 3.x installed

### Container Registry
- Azure Container Registry (ACR) or other container registry
- Images built and pushed to registry

### Domain & SSL
- Domain name for external access (**optional** - can deploy for internal access only)
- SSL certificate (optional, can use Let's Encrypt with cert-manager)

## Quick Start

### 1. Build and Push Container Images

```bash
# Set your registry
export REGISTRY="your-registry.azurecr.io"

# Build and push MCPServer
docker build -f src/MCPServer/Dockerfile -t $REGISTRY/mcpserver:latest .
docker push $REGISTRY/mcpserver:latest

# Build and push ApprovalService
docker build -f src/ApprovalService/Dockerfile -t $REGISTRY/approval-service:latest .
docker push $REGISTRY/approval-service:latest

# Build and push AgentAlpha
docker build -f src/Agent/AgentAlpha/Dockerfile -t $REGISTRY/agent-alpha:latest .
docker push $REGISTRY/agent-alpha:latest
```

### 2. Deploy with Helm

```bash
# Install/upgrade the chart
helm install agents ./helm/agents \
  --set global.registry=$REGISTRY \
  --set secrets.openaiApiKey="your-openai-api-key" \
  --set secrets.restAuthToken="your-auth-token" \
  --set ingress.hosts[0].host="agents.yourdomain.com" \
  --set ingress.tls[0].hosts[0]="agents.yourdomain.com" \
  --namespace agents \
  --create-namespace
```

### 3. Deploy with Kustomize

```bash
# Edit the image references in overlays/aks/kustomization.yaml
# Update domain name in overlays/aks/ingress-patch.yaml
# Update secrets in base/secrets.yaml with base64 encoded values

# Apply the configuration
kubectl apply -k k8s/overlays/aks
```

## Configuration

### Environment Variables

The applications use the following environment variables (configured via ConfigMap):

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Production | ASP.NET Core environment |
| `MCP_TRANSPORT` | sse | Transport type (stdio/sse) |
| `LOGGING__LOGLEVEL__DEFAULT` | Information | Default log level |
| `AGENT_MAX_ITERATIONS` | 10 | Maximum agent iterations |
| `AGENT_MODEL` | gpt-4.1-nano | Default OpenAI model for AgentAlpha |
| `AGENT_TEMPERATURE` | 0.7 | Default temperature for AgentAlpha responses |
| `AGENT_TIMEOUT_MINUTES` | 5 | Default timeout for AgentAlpha tasks |
| `APPROVAL_PROVIDER_TYPE` | Rest | Approval provider type |

### Secrets

Sensitive configuration is stored in Kubernetes secrets:

- `OPENAI_API_KEY`: OpenAI API key for AI functionality
- `REST_AUTH_TOKEN`: Authentication token for REST approval service

## Services

### MCPServer
- **Port**: 3000
- **Protocol**: HTTP
- **Purpose**: Model Context Protocol server with mathematical tools

### ApprovalService  
- **Port**: 5000
- **Protocol**: HTTP
- **Purpose**: Web dashboard for tool approvals
- **UI**: Available at `http://your-domain/` 

### SessionService
- **Port**: 5001
- **Protocol**: HTTP
- **Purpose**: HTTP API for managing agent sessions and conversation state
- **API**: Available at `http://your-domain/api/sessions` 

## Ingress Configuration

The ingress can be configured for either:

### Internal Access (No Domain Required)
- **Default Configuration**: For internal access via port-forwarding
- **Root Path (/)**: Routes to ApprovalService dashboard
- **Sessions Path (/api/sessions)**: Routes to SessionService API
- **MCP Path (/mcp)**: Routes to MCPServer
- **Access Method**: Use `kubectl port-forward` to access services

### External Access (Domain Required)
- **Custom Configuration**: For external access with domain
- **Root Path (/)**: Routes to ApprovalService dashboard  
- **Sessions Path (/api/sessions)**: Routes to SessionService API
- **MCP Path (/mcp)**: Routes to MCPServer
- **Access Method**: Direct HTTP/HTTPS access via domain

### SSL/TLS

For production deployments:

1. **Option 1**: Use cert-manager for automatic Let's Encrypt certificates
2. **Option 2**: Upload your SSL certificate to AKS and reference it in the ingress

```bash
# Create TLS secret manually
kubectl create secret tls agents-tls-secret \
  --cert=path/to/cert.crt \
  --key=path/to/cert.key \
  --namespace agents
```

## Persistence

The ApprovalService uses SQLite database stored on persistent volume:

- **StorageClass**: `managed-csi` (Azure Managed Disks)
- **Size**: 1Gi (configurable)
- **AccessMode**: ReadWriteOnce

## Monitoring & Logging

### Health Checks

- **MCPServer**: TCP probe on port 3000
- **ApprovalService**: HTTP probe on port 5000 at `/`

### Logs

View logs using kubectl:

```bash
# MCPServer logs
kubectl logs -f deployment/agents-mcpserver -n agents

# ApprovalService logs  
kubectl logs -f deployment/agents-approval-service -n agents

# Agent job logs
kubectl logs job/agents-agent-alpha-job -n agents
```

## Scaling

### MCPServer & ApprovalService

Both services can be scaled horizontally:

```bash
# Scale MCPServer
kubectl scale deployment agents-mcpserver --replicas=3 -n agents

# Scale ApprovalService
kubectl scale deployment agents-approval-service --replicas=2 -n agents
```

### AgentAlpha

AgentAlpha runs as jobs and now supports comprehensive parameter configuration:

- **Job**: One-time execution
- **CronJob**: Scheduled execution (default: every 6 hours)

#### Enhanced Configuration

Configure in Helm values:

```yaml
agentAlpha:
  type: cronjob  # or 'job'
  cronjob:
    schedule: "0 */6 * * *"
    suspend: false
  
  # Task and parameters
  task: "Monitor cluster health and report status"
  parameters:
    model: "gpt-4o"           # OpenAI model
    temperature: 0.7          # Response creativity (0.0-1.0)
    maxIterations: 15         # Conversation iterations
    priority: "High"          # Task priority
    timeoutMinutes: 10        # Task timeout
    verboseLogging: true      # Enable detailed logging
    systemPrompt: "You are a Kubernetes monitoring specialist"
```

#### Parameter Examples

```bash
# Deploy with GPT-4 and high creativity
helm install agents ./helm/agents \
  --set agentAlpha.parameters.model="gpt-4o" \
  --set agentAlpha.parameters.temperature=0.9

# Deploy with custom system prompt
helm install agents ./helm/agents \
  --set agentAlpha.task="Analyze performance metrics" \
  --set agentAlpha.parameters.systemPrompt="You are a performance analyst"

# Deploy with resource optimization
helm install agents ./helm/agents \
  --set agentAlpha.parameters.model="gpt-4.1-nano" \
  --set agentAlpha.parameters.maxIterations=5 \
  --set agentAlpha.parameters.timeoutMinutes=3
```

#### Kustomize Overlays

The AKS overlay demonstrates environment-specific configuration:

```yaml
# k8s/overlays/aks/agent-alpha-patch.yaml
spec:
  template:
    spec:
      containers:
      - name: agent-alpha
        args: ["--model", "gpt-4o", "--temperature", "0.8", "--priority", "High", "--verbose", "Environment-specific task"]
```

## Troubleshooting

### Common Issues

1. **Images not found**: Ensure images are pushed to registry and registry is accessible
2. **Pods failing**: Check resource limits and node capacity
3. **Ingress not working**: Verify Application Gateway configuration and DNS
4. **Database errors**: Check persistent volume claims and storage class

### Debugging Commands

```bash
# Check pod status
kubectl get pods -n agents

# Describe problematic pod
kubectl describe pod <pod-name> -n agents

# Check events
kubectl get events -n agents --sort-by=.metadata.creationTimestamp

# Test service connectivity
kubectl run test-pod --rm -i --tty --image=busybox -- /bin/sh
# Inside pod: wget -qO- http://agents-mcpserver:3000
```

## Security

### Pod Security

- Containers run as non-root user (UID 1000)
- Security contexts configured with minimal privileges
- ReadOnlyRootFilesystem where possible

### Network Security

- Services use ClusterIP (internal only) except via ingress
- Network policies can be added for additional isolation

### Secrets Management

- Kubernetes secrets for sensitive data
- Consider using Azure Key Vault integration for enhanced security

## Maintenance

### Updates

```bash
# Update Helm chart
helm upgrade agents ./helm/agents \
  --reuse-values \
  --set images.mcpserver.tag=new-version

# Update via Kustomize
# Edit image tags in kustomization.yaml
kubectl apply -k k8s/overlays/aks
```

### Backup

Backup persistent data:

```bash
# Backup ApprovalService database
kubectl exec deployment/agents-approval-service -n agents -- \
  tar czf - /app/data | \
  kubectl exec -i backup-pod -- tar xzf - -C /backup
```

## Cost Optimization

### Resource Requests/Limits

Configure appropriate resource requests and limits:

```yaml
resources:
  requests:
    memory: "128Mi"
    cpu: "100m"
  limits:
    memory: "256Mi" 
    cpu: "200m"
```

### Node Pools

Consider using:
- **System node pool**: For system workloads
- **User node pool**: For application workloads with spot instances

### Horizontal Pod Autoscaler

Enable HPA for dynamic scaling:

```bash
kubectl autoscale deployment agents-mcpserver \
  --cpu-percent=80 \
  --min=1 \
  --max=5 \
  -n agents
```