# Repository: agents
Type: .NET
Solution: agents.sln
Components:
  - blueprints/mcp-server — McpServer (net10.0), 1 file
  - blueprints/research-agent/ResearchAgent.Plugins — ResearchAgent.Plugins (net10.0)
  - blueprints/research-agent/ResearchAgent.App — ResearchAgent.App (net10.0), 3 files
  - blueprints/research-agent/ResearchAgent.Core — ResearchAgent.Core (net10.0)
  - blueprints/coding-agent/src/Forge.Core — Forge.Core (net10.0), 15 files
  - blueprints/coding-agent/src/Forge.App — Forge.App (net10.0), 1 file
  - blueprints/life-agent/src/LifeAgent.App — LifeAgent.App (net10.0), 3 files
  - blueprints/life-agent/src/LifeAgent.Core — LifeAgent.Core (net10.0), 6 files
  - blueprints/life-agent/src/LifeAgent.Audio — LifeAgent.Audio (net10.0), 2 files
Test projects: Forge.Tests
Build: dotnet build agents.sln
Test: dotnet test blueprints/coding-agent/src/Forge.Tests