#!/bin/bash

echo "=== Checking Logging Consistency in AgentAlpha V2 Components ==="
echo

# Define V2 components to check
V2_COMPONENTS=(
    "TaskRouter"
    "FastPathExecutor"
    "ChainedPlanner"
    "PlanEvaluator"
    "PlanRefinementLoop"
    "WorkerConversation"
)

echo "Checking for ILogger injection and usage in V2 components..."
echo "============================================================"

for component in "${V2_COMPONENTS[@]}"; do
    echo
    echo "Component: $component"
    echo "-------------------"
    
    # Find the file
    file=$(find /home/cq/p/agents/src/Agent/AgentAlpha -name "${component}.cs" -type f | head -1)
    
    if [ -z "$file" ]; then
        echo "  ⚠️  File not found!"
        continue
    fi
    
    echo "  File: $file"
    
    # Check for ILogger field
    if grep -q "ILogger<${component}>" "$file"; then
        echo "  ✓ Has ILogger<${component}> field"
    else
        echo "  ✗ Missing ILogger<${component}> field"
    fi
    
    # Check for logger in constructor
    if grep -q "_logger = logger" "$file"; then
        echo "  ✓ Logger injected in constructor"
    else
        echo "  ✗ Logger not properly injected"
    fi
    
    # Count log statements
    log_count=$(grep -c "_logger.Log" "$file" 2>/dev/null || echo 0)
    echo "  📊 Log statements found: $log_count"
    
    # Check for different log levels
    echo "  📋 Log levels used:"
    grep -o "_logger.Log[A-Za-z]*" "$file" 2>/dev/null | sort | uniq -c | sed 's/^/    /'
done

echo
echo "=== Logging Pattern Analysis ==="
echo

# Check for consistent error handling pattern
echo "Error handling with logging:"
echo "---------------------------"
for component in "${V2_COMPONENTS[@]}"; do
    file=$(find /home/cq/p/agents/src/Agent/AgentAlpha -name "${component}.cs" -type f | head -1)
    if [ -n "$file" ]; then
        echo
        echo "$component:"
        grep -B2 -A2 "LogError" "$file" 2>/dev/null | head -10 || echo "  No LogError found"
    fi
done

echo
echo "=== Summary ==="
echo "Check complete. Review the output above for any inconsistencies."
