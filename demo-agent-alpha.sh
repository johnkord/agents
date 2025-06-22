#!/bin/bash

# AgentAlpha Enhanced Capabilities Demo
# This script demonstrates the enhanced AgentAlpha features

echo "🤖 AgentAlpha Enhanced Capabilities Demo"
echo "========================================"
echo

# Navigate to the agent directory
cd "$(dirname "$0")/../src/Agent/AgentAlpha" || exit 1

echo "1️⃣  Testing MCP Server connectivity..."
dotnet run "test"
echo

echo "2️⃣  Demo: System Information"
echo "Task: Get current time and system info"
echo "Note: This requires OPENAI_API_KEY to be set"
echo

if [ -z "$OPENAI_API_KEY" ]; then
    echo "⚠️  OPENAI_API_KEY not set. Set it to run AI-powered demos:"
    echo "   export OPENAI_API_KEY=your_key_here"
    echo
else
    echo "Running: What time is it and what system are we on?"
    echo "What time is it and what system are we on?" | timeout 30s dotnet run
    echo
fi

echo "3️⃣  Available Enhanced Tools:"
echo "📁 File Operations: read_file, write_file, list_directory, file_exists"
echo "📝 Text Processing: search_text, replace_text, word_count, format_text"  
echo "🖥️  System Info: get_current_time, get_system_info, get_environment_variable"
echo "🔢 Math Operations: add, subtract, multiply, divide"
echo

echo "4️⃣  Example Task Ideas:"
echo "• 'List the current directory and count files'"
echo "• 'Create a file called hello.txt with greeting message'"
echo "• 'Read a file and count the words in it'"
echo "• 'Search for TODO items in project files'"
echo "• 'Get system information and current time'"
echo

echo "✅ Demo complete! AgentAlpha is now much more capable and useful."
echo "📖 See docs/agent-alpha-enhancement-plan.md for full details."