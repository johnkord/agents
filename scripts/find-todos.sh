#!/bin/bash

echo "=== Finding TODO comments in AgentAlpha codebase ==="
echo

# Search for TODO comments in C# files
echo "C# Files with TODOs:"
echo "===================="
find /home/cq/p/agents/src/Agent/AgentAlpha -name "*.cs" -type f -exec grep -l "TODO" {} \; 2>/dev/null | while read file; do
    echo
    echo "File: $file"
    grep -n "TODO" "$file" | sed 's/^/  Line /'
done

# Search for TODO in markdown docs
echo
echo "Documentation with TODOs:"
echo "========================"
find /home/cq/p/agents/docs -name "*.md" -type f -exec grep -l "TODO" {} \; 2>/dev/null | while read file; do
    echo
    echo "File: $file"
    grep -n "TODO" "$file" | sed 's/^/  Line /'
done

# Search for FIXME, HACK, XXX comments as well
echo
echo "Other markers (FIXME, HACK, XXX):"
echo "================================="
find /home/cq/p/agents/src/Agent/AgentAlpha -name "*.cs" -type f -exec grep -E -n "(FIXME|HACK|XXX)" {} + 2>/dev/null | sed 's/^/  /'

echo
echo "=== Scan complete ==="
