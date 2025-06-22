#!/bin/bash

# Deploy Agents to AKS Cluster
# This script deploys the MCP Server and AgentAlpha to an AKS cluster

set -e

echo "🚀 Deploying Agents to AKS Cluster"
echo "=================================="

# Function to check if kubectl is available
check_kubectl() {
    if ! command -v kubectl &> /dev/null; then
        echo "❌ kubectl is not installed or not in PATH"
        echo "Please install kubectl and configure it to connect to your AKS cluster"
        exit 1
    fi
}

# Function to check if cluster is accessible
check_cluster() {
    echo "🔍 Checking cluster connectivity..."
    if ! kubectl cluster-info &> /dev/null; then
        echo "❌ Cannot connect to Kubernetes cluster"
        echo "Please ensure kubectl is configured to connect to your AKS cluster"
        exit 1
    fi
    echo "✅ Cluster connectivity confirmed"
}

# Function to create or update secret with OpenAI API key
setup_openai_secret() {
    read -p "🔑 Enter your OpenAI API Key: " -s OPENAI_API_KEY
    echo ""
    
    if [ -z "$OPENAI_API_KEY" ]; then
        echo "❌ OpenAI API Key is required"
        exit 1
    fi
    
    echo "🔐 Creating OpenAI API Key secret..."
    kubectl create secret generic agents-secrets \
        --from-literal=OPENAI_API_KEY="$OPENAI_API_KEY" \
        --namespace=agents \
        --dry-run=client -o yaml | kubectl apply -f -
    echo "✅ Secret created/updated"
}

# Function to deploy manifests
deploy_manifests() {
    echo "📦 Deploying Kubernetes manifests..."
    
    # Apply namespace and config first
    kubectl apply -f k8s/namespace-and-config.yaml
    echo "✅ Namespace and ConfigMap applied"
    
    # Deploy MCP Server
    kubectl apply -f k8s/mcp-server.yaml
    echo "✅ MCP Server deployed"
    
    # Wait for MCP Server to be ready
    echo "⏳ Waiting for MCP Server to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/mcp-server -n agents
    echo "✅ MCP Server is ready"
    
    # Deploy AgentAlpha
    kubectl apply -f k8s/agent-alpha.yaml
    echo "✅ AgentAlpha deployed"
    
    echo "⏳ Waiting for AgentAlpha to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/agent-alpha -n agents
    echo "✅ AgentAlpha is ready"
}

# Function to show deployment status
show_status() {
    echo ""
    echo "📊 Deployment Status"
    echo "==================="
    kubectl get all -n agents
    
    echo ""
    echo "🔍 Pod Details"
    echo "=============="
    kubectl get pods -n agents -o wide
    
    echo ""
    echo "📋 Recent Events"
    echo "==============="
    kubectl get events -n agents --sort-by='.firstTimestamp' | tail -10
}

# Function to show logs
show_logs() {
    echo ""
    echo "📋 Recent Logs"
    echo "=============="
    echo "MCP Server logs:"
    kubectl logs -n agents deployment/mcp-server --tail=20 || echo "No logs available yet"
    
    echo ""
    echo "AgentAlpha logs:"
    kubectl logs -n agents deployment/agent-alpha --tail=20 || echo "No logs available yet"
}

# Function to run a test job
run_test_job() {
    echo ""
    echo "🧪 Running test job..."
    kubectl apply -f k8s/agent-alpha-job.yaml
    
    echo "⏳ Waiting for job to complete..."
    kubectl wait --for=condition=complete --timeout=300s job/agent-alpha-task -n agents || true
    
    echo "📋 Job logs:"
    kubectl logs -n agents job/agent-alpha-task
    
    # Cleanup job
    kubectl delete job agent-alpha-task -n agents
}

# Main deployment flow
main() {
    check_kubectl
    check_cluster
    setup_openai_secret
    deploy_manifests
    show_status
    show_logs
    
    # Ask if user wants to run a test
    echo ""
    read -p "🤔 Would you like to run a test job? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        run_test_job
    fi
    
    echo ""
    echo "🎉 Deployment completed successfully!"
    echo ""
    echo "📖 Next steps:"
    echo "  • Monitor pods: kubectl get pods -n agents -w"
    echo "  • View logs: kubectl logs -n agents deployment/mcp-server"
    echo "  • View logs: kubectl logs -n agents deployment/agent-alpha"
    echo "  • Run custom job: modify k8s/agent-alpha-job.yaml and apply"
    echo "  • Scale deployment: kubectl scale deployment agent-alpha --replicas=3 -n agents"
    echo ""
    echo "🗑️  To clean up: kubectl delete namespace agents"
}

# Help function
show_help() {
    echo "Deploy Agents to AKS Cluster"
    echo ""
    echo "Usage: $0 [OPTION]"
    echo ""
    echo "Options:"
    echo "  -h, --help     Show this help message"
    echo "  -s, --status   Show deployment status only"
    echo "  -l, --logs     Show logs only"
    echo "  -t, --test     Run test job only"
    echo "  -c, --cleanup  Delete the agents namespace"
    echo ""
    echo "Prerequisites:"
    echo "  • kubectl installed and configured for your AKS cluster"
    echo "  • Docker images built and pushed to a registry accessible by AKS"
    echo "  • Update image references in k8s/*.yaml files"
}

# Cleanup function
cleanup() {
    echo "🗑️  Cleaning up agents namespace..."
    kubectl delete namespace agents --ignore-not-found=true
    echo "✅ Cleanup completed"
}

# Parse command line arguments
case "${1:-}" in
    -h|--help)
        show_help
        exit 0
        ;;
    -s|--status)
        check_kubectl
        check_cluster
        show_status
        exit 0
        ;;
    -l|--logs)
        check_kubectl
        check_cluster
        show_logs
        exit 0
        ;;
    -t|--test)
        check_kubectl
        check_cluster
        run_test_job
        exit 0
        ;;
    -c|--cleanup)
        check_kubectl
        check_cluster
        cleanup
        exit 0
        ;;
    "")
        main
        ;;
    *)
        echo "❌ Unknown option: $1"
        show_help
        exit 1
        ;;
esac