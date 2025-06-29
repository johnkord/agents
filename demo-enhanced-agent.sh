#!/bin/bash

# Enhanced Agent Input Parameters Demo Script
# This script demonstrates the new command-line capabilities of AgentAlpha

echo "=== Enhanced Agent Input Parameters Demo ==="
echo ""

echo "1. Basic usage (unchanged):"
echo "   dotnet run \"Calculate 2 + 2\""
echo ""

echo "2. With specific model:"
echo "   dotnet run --model \"gpt-4.1-nano\" \"Write a haiku\""
echo ""

echo "3. With temperature control:"
echo "   dotnet run --temperature 0.2 \"Precise calculation: 123 * 456\""
echo "   dotnet run --temperature 0.8 \"Creative story about AI\""
echo ""

echo "4. With custom system prompt:"
echo "   dotnet run --system-prompt \"You are a math tutor\" \"Help me understand fractions\""
echo ""

echo "5. With priority and timeout:"
echo "   dotnet run --priority High --timeout 5 \"Urgent: What's the capital of France?\""
echo ""

echo "6. With verbose logging:"
echo "   dotnet run --verbose \"Debug this calculation: 100 / 5\""
echo ""

echo "7. Complex example with multiple parameters:"
echo "   dotnet run --model \"gpt-4.1\" --temperature 0.7 --priority High --max-iterations 3 --verbose \"Create a short poem about technology\""
echo ""

echo "8. Testing MCP connection (no API key required):"
echo "   dotnet run \"test\""
echo ""

echo "=== Parameter Reference ==="
echo ""
echo "| Parameter         | Short | Description                     | Example             |"
echo "|-------------------|-------|---------------------------------|---------------------|"
echo "| --model           | -m    | OpenAI model to use            | gpt-4.1              |"
echo "| --temperature     | -t    | Response creativity (0.0-1.0)  | 0.7                 |"
echo "| --max-iterations  |       | Max conversation loops         | 5                   |"
echo "| --priority        |       | Task priority level             | High                |"
echo "| --timeout         |       | Execution timeout in minutes   | 10                  |"
echo "| --verbose         | -v    | Enable detailed logging         | (flag, no value)    |"
echo "| --system-prompt   |       | Custom system prompt           | \"You are a tutor\"  |"
echo ""

echo "=== Usage Notes ==="
echo "- Set OPENAI_API_KEY environment variable before running (except for 'test' command)"
echo "- All parameters are optional and have sensible defaults"
echo "- Backwards compatibility: simple 'dotnet run \"task\"' still works"
echo "- Parameters can be combined in any order"
echo "- Temperature is clamped to 0.0-1.0 range"
echo "- Timeout is specified in minutes"
echo ""

echo "Try running: dotnet run --help (if implemented) or any of the examples above!"