# Complete AKS Deployment Guide: From Zero to Running Agents

This guide provides complete step-by-step instructions to deploy the Agents MCP system to an existing Azure Kubernetes Service (AKS) cluster. Follow these commands on your Linux machine to get everything built, containerized, deployed, and running.

## Prerequisites

### What You Need
- **Existing AKS cluster** (already deployed)
- **Azure Container Registry (ACR)** access
- **Linux machine** with these tools installed:
  - [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
  - [kubectl](https://kubernetes.io/docs/tasks/tools/)
  - [Helm 3.x](https://helm.sh/docs/intro/install/)
  - [Docker](https://docs.docker.com/get-docker/)
  - [git](https://git-scm.com/downloads)
- **OpenAI API Key**
- **Domain name** for external access (optional but recommended)

### Verify Tools Installation
```bash
# Check all required tools are installed
az --version
kubectl version --client
helm version
docker --version
git --version
```

## Step 1: Set Up Environment Variables

```bash
# Azure and Kubernetes configuration
export RESOURCE_GROUP="your-resource-group"        # Your AKS resource group
export AKS_NAME="your-aks-cluster"                 # Your AKS cluster name
export ACR_NAME="your-registry"                    # Your ACR name (without .azurecr.io)
export REGISTRY="$ACR_NAME.azurecr.io"             # Full registry URL

# Application configuration
export DOMAIN="agents.yourdomain.com"              # Your domain (or use external IP)
export OPENAI_API_KEY="sk-your-openai-api-key"     # Your OpenAI API key
export NAMESPACE="agents"                          # Kubernetes namespace

# Verify variables
echo "Resource Group: $RESOURCE_GROUP"
echo "AKS Cluster: $AKS_NAME"
echo "Registry: $REGISTRY"
echo "Domain: $DOMAIN"
echo "Namespace: $NAMESPACE"
```

## Step 2: Connect to Your AKS Cluster

```bash
# Login to Azure
az login

# Get AKS credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME

# Verify connection
kubectl get nodes

# Create namespace
kubectl create namespace $NAMESPACE --dry-run=client -o yaml | kubectl apply -f -
```

## Step 3: Set Up Container Registry Access

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Verify ACR access
az acr list --resource-group $RESOURCE_GROUP --query "[].{Name:name,LoginServer:loginServer}" --output table

# Test docker push access
docker pull hello-world
docker tag hello-world $REGISTRY/test:latest
docker push $REGISTRY/test:latest
docker rmi $REGISTRY/test:latest
az acr repository delete --name $ACR_NAME --repository test --yes
```

## Step 4: Clone and Build the Application

```bash
# Clone the repository
git clone https://github.com/johnkord/agents.git
cd agents

# Make scripts executable
chmod +x scripts/*.sh

# Build and push all container images
./scripts/build-and-push.sh $REGISTRY

# Verify images were pushed
az acr repository list --name $ACR_NAME --output table
```

Expected output should show:
- `mcpserver`
- `approval-service`
- `agent-alpha`
- `mcpclient`

## Step 5: Deploy Applications with Helm

### Option A: Quick Deploy (Recommended)
```bash
# Deploy everything with one command
./scripts/helm-deploy.sh $REGISTRY $DOMAIN $OPENAI_API_KEY
```

### Option B: Manual Helm Deploy (For customization)
```bash
# Create custom values file
cat > values-custom.yaml << EOF
global:
  registry: $REGISTRY

secrets:
  openaiApiKey: "$OPENAI_API_KEY"
  restAuthToken: "$(openssl rand -base64 32)"

ingress:
  hosts:
    - host: $DOMAIN
      paths:
        - path: /
          pathType: Prefix
          service:
            name: approval-service
            port: 5000
        - path: /mcp
          pathType: Prefix
          service:
            name: mcpserver
            port: 3000

agentAlpha:
  type: cronjob
  cronjob:
    schedule: "0 */6 * * *"  # Every 6 hours
  task: "Monitor system health and report status"
  parameters:
    model: "gpt-4o"
    temperature: 0.7
    maxIterations: 15
    verboseLogging: true
EOF

# Deploy with Helm
helm upgrade --install agents ./helm/agents \
  --values values-custom.yaml \
  --namespace $NAMESPACE \
  --wait \
  --timeout=300s
```

## Step 6: Verify Deployment

```bash
# Check all pods are running
kubectl get pods -n $NAMESPACE

# Check services
kubectl get services -n $NAMESPACE

# Check ingress
kubectl get ingress -n $NAMESPACE

# Check persistent volume claims
kubectl get pvc -n $NAMESPACE

# Wait for all pods to be ready
kubectl wait --for=condition=ready pod --all -n $NAMESPACE --timeout=300s
```

Expected output:
- `agents-mcpserver-xxx` - Running
- `agents-approval-service-xxx` - Running
- PVCs for persistent storage

## Step 7: Configure External Access

### Get External IP
```bash
# Get the external IP assigned to your ingress
EXTERNAL_IP=$(kubectl get ingress agents-ingress -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo "External IP: $EXTERNAL_IP"

# If using a domain, add DNS A record pointing to this IP
# If not using a domain, you can access via IP directly
```

### Test Basic Connectivity
```bash
# Test ApprovalService dashboard (adjust URL as needed)
curl -k "http://$EXTERNAL_IP/" -I

# Test MCP Server
curl -k "http://$EXTERNAL_IP/mcp/" -I

# If using HTTPS/domain
curl -k "https://$DOMAIN/" -I
curl -k "https://$DOMAIN/mcp/" -I
```

## Step 8: Access the Tool Approval Dashboard

### Open in Browser
```bash
# Print access URLs
echo "Tool Approval Dashboard: https://$DOMAIN/"
echo "MCP Server: https://$DOMAIN/mcp"
echo ""
echo "Or using IP directly:"
echo "Tool Approval Dashboard: http://$EXTERNAL_IP/"
echo "MCP Server: http://$EXTERNAL_IP/mcp"
```

### Navigate the Dashboard
1. Open `https://$DOMAIN/` in your browser
2. You should see the Tool Approval Service interface
3. The dashboard shows pending tool approvals from agents
4. You can approve or deny tool execution requests

## Step 9: Check Logs and Monitor Components

### View Application Logs
```bash
# MCPServer logs
kubectl logs -f deployment/agents-mcpserver -n $NAMESPACE

# ApprovalService logs
kubectl logs -f deployment/agents-approval-service -n $NAMESPACE

# Agent logs (if job is running)
kubectl logs -f job/agents-agent-alpha-job -n $NAMESPACE

# Or stream all logs
kubectl logs -f -l app.kubernetes.io/name=agents -n $NAMESPACE --all-containers=true
```

### Monitor Resource Usage
```bash
# Check resource usage
kubectl top nodes
kubectl top pods -n $NAMESPACE

# Check events
kubectl get events -n $NAMESPACE --sort-by=.metadata.creationTimestamp

# Check persistent storage
kubectl describe pvc -n $NAMESPACE
```

## Step 10: Trigger Agent Runs

### Method 1: Run Agent Job Manually
```bash
# Create a one-time job with custom task
cat > agent-job.yaml << EOF
apiVersion: batch/v1
kind: Job
metadata:
  name: agents-manual-task-$(date +%s)
  namespace: $NAMESPACE
spec:
  template:
    spec:
      restartPolicy: OnFailure
      containers:
      - name: agent-alpha
        image: $REGISTRY/agent-alpha:latest
        command: ["dotnet", "AgentAlpha.dll"]
        args:
          - "--model"
          - "gpt-4o"
          - "--temperature"
          - "0.7"
          - "--max-iterations"
          - "10"
          - "--verbose"
          - "Analyze the current system health and provide recommendations"
        env:
        - name: MCP_SERVER_URL
          value: "http://agents-mcpserver:3000"
        - name: REST_BASE_URL
          value: "http://agents-approval-service:5000"
        - name: AGENT_SESSION_DB_PATH
          value: "/app/data/agent_sessions.db"
        envFrom:
        - configMapRef:
            name: agents-config
        - secretRef:
            name: agents-secrets
        volumeMounts:
        - name: agent-data
          mountPath: /app/data
      volumes:
      - name: agent-data
        persistentVolumeClaim:
          claimName: agents-agent-data
EOF

# Apply the job
kubectl apply -f agent-job.yaml

# Watch the job
kubectl get jobs -n $NAMESPACE -w
```

### Method 2: Configure CronJob Schedule
```bash
# Update CronJob schedule to run more frequently
helm upgrade agents ./helm/agents \
  --reuse-values \
  --set agentAlpha.type=cronjob \
  --set agentAlpha.cronjob.schedule="*/15 * * * *" \
  --set agentAlpha.task="Monitor cluster resources and check for anomalies" \
  --namespace $NAMESPACE
```

### Method 3: Run Agent with Custom Parameters
```bash
# Run agent with specific parameters via Helm
helm upgrade agents ./helm/agents \
  --reuse-values \
  --set agentAlpha.type=job \
  --set agentAlpha.task="Analyze application logs and summarize findings" \
  --set agentAlpha.parameters.model="gpt-4o" \
  --set agentAlpha.parameters.maxIterations=20 \
  --set agentAlpha.parameters.verboseLogging=true \
  --namespace $NAMESPACE
```

### Check Agent Execution
```bash
# Watch for new agent jobs
kubectl get jobs -n $NAMESPACE

# Follow agent logs
LATEST_JOB=$(kubectl get jobs -n $NAMESPACE --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}')
kubectl logs -f job/$LATEST_JOB -n $NAMESPACE

# Check agent session data (persisted)
kubectl exec deployment/agents-approval-service -n $NAMESPACE -- ls -la /app/data/
```

## Step 11: Verify Database Persistence

```bash
# Check that databases are persisted
kubectl get pvc -n $NAMESPACE

# Test persistence by restarting pods
kubectl rollout restart deployment/agents-approval-service -n $NAMESPACE
kubectl rollout restart deployment/agents-mcpserver -n $NAMESPACE

# Wait for rollout
kubectl rollout status deployment/agents-approval-service -n $NAMESPACE
kubectl rollout status deployment/agents-mcpserver -n $NAMESPACE

# Verify data is still there
kubectl exec deployment/agents-approval-service -n $NAMESPACE -- ls -la /app/data/
```

## Common Tasks

### Scale Components
```bash
# Scale MCPServer
kubectl scale deployment agents-mcpserver --replicas=3 -n $NAMESPACE

# Scale ApprovalService
kubectl scale deployment agents-approval-service --replicas=2 -n $NAMESPACE
```

### Update Applications
```bash
# Rebuild and push new images
./scripts/build-and-push.sh $REGISTRY

# Update deployment
helm upgrade agents ./helm/agents \
  --reuse-values \
  --namespace $NAMESPACE
```

### Backup Database
```bash
# Create backup of approval service database
kubectl exec deployment/agents-approval-service -n $NAMESPACE -- \
  cp /app/data/approval_service.db /tmp/approval_backup_$(date +%Y%m%d_%H%M%S).db

# Copy backup to local machine
kubectl cp $NAMESPACE/$(kubectl get pod -l app.kubernetes.io/component=approval-service -n $NAMESPACE -o jsonpath='{.items[0].metadata.name}'):/tmp/approval_backup_*.db ./
```

## Troubleshooting

### Common Issues

1. **Pods stuck in Pending**
   ```bash
   kubectl describe pod <pod-name> -n $NAMESPACE
   kubectl get events -n $NAMESPACE
   ```

2. **Image pull errors**
   ```bash
   # Check ACR permissions
   az acr show --name $ACR_NAME --query "{Name:name,LoginServer:loginServer,AdminUserEnabled:adminUserEnabled}"
   
   # Verify images exist
   az acr repository list --name $ACR_NAME
   ```

3. **Ingress not working**
   ```bash
   kubectl describe ingress agents-ingress -n $NAMESPACE
   kubectl get svc -n ingress-azure
   ```

4. **Database connection errors**
   ```bash
   kubectl describe pvc -n $NAMESPACE
   kubectl exec deployment/agents-approval-service -n $NAMESPACE -- df -h /app/data
   ```

### Debug Commands
```bash
# Shell into running pods
kubectl exec -it deployment/agents-mcpserver -n $NAMESPACE -- /bin/sh
kubectl exec -it deployment/agents-approval-service -n $NAMESPACE -- /bin/sh

# Check configuration
kubectl get configmap agents-config -n $NAMESPACE -o yaml
kubectl get secret agents-secrets -n $NAMESPACE -o yaml

# Port forward for local testing
kubectl port-forward svc/agents-approval-service 5000:5000 -n $NAMESPACE
kubectl port-forward svc/agents-mcpserver 3000:3000 -n $NAMESPACE
```

## Success Criteria

After completing this guide, you should have:

✅ All applications running in AKS
✅ Persistent SQLite databases across pod restarts  
✅ Working Tool Approval Dashboard accessible via browser
✅ MCP Server responding to requests
✅ Agent jobs executing and generating logs
✅ Database persistence verified across pod restarts
✅ Ability to trigger new agent runs with custom tasks and parameters

Your Agents MCP system is now fully deployed and operational on AKS!