# GitHub Copilot Customization Guide

> Research compiled from official GitHub Docs, VS Code Docs, and agentskills.io (March 2026)

## Overview

GitHub Copilot supports several layered customization mechanisms. From simplest to most powerful:

| Mechanism | Purpose | Scope | Portability |
|---|---|---|---|
| **Custom Instructions** | Coding standards & guidelines | Always-on or file-pattern-based | VS Code + GitHub.com |
| **Agent Skills** | Specialized capabilities & workflows | On-demand, task-matched | Cross-agent (open standard) |
| **Prompt Files** | Reusable slash commands | Invoked manually | VS Code |
| **Custom Agents** | Specialized AI personas | Selected or delegated | VS Code |
| **MCP Servers** | External API/database connections | Tool-matched | VS Code |
| **Hooks** | Lifecycle automation scripts | Event-triggered | VS Code |

---

## 1. Custom Instructions

Custom instructions are Markdown files that automatically influence how Copilot generates code and handles tasks. They are injected into the context for every request without needing to be mentioned explicitly.

### 1a. Repository-Wide Instructions (`copilot-instructions.md`)

**Location:** `.github/copilot-instructions.md`

Automatically applied to **all** chat requests in the workspace. Use for project-wide coding standards, architecture decisions, and conventions.

```markdown
# Project Conventions

- Use TypeScript with strict mode enabled
- Prefer `date-fns` over `moment.js` (moment is deprecated, larger bundle)
- All API responses follow the `{ data, error, meta }` envelope pattern
- Use Zod for runtime validation of external inputs
```

**Key facts:**
- VS Code auto-detects this file — no configuration needed
- Also supported by GitHub.com Copilot Chat and Copilot coding agent
- You can generate one automatically with the `/init` slash command in VS Code chat
- Instructions should be ≤2 pages; concise and self-contained

### 1b. Path-Specific Instructions (`.instructions.md` files)

**Location:** `.github/instructions/` directory (configurable via `chat.instructionsFilesLocations` setting)

Applied conditionally based on file patterns (via `applyTo` glob) or semantic matching of the description to the current task.

```markdown
---
name: 'Python Standards'
description: 'Coding conventions for Python files'
applyTo: '**/*.py'
---
# Python coding standards
- Follow PEP 8 style guide
- Use type hints for all function signatures
- Write docstrings for public functions
```

**Frontmatter fields:**

| Field | Required | Description |
|---|---|---|
| `name` | No | Display name in UI. Defaults to filename. |
| `description` | No | Short description shown on hover. |
| `applyTo` | No | Glob pattern for auto-matching. If omitted, won't auto-apply. |

**Glob examples:**
- `**/*.py` — all Python files recursively
- `src/**/*.ts` — TypeScript files under `src/`
- `**/*.test.js,**/*.spec.js` — multiple patterns (comma-separated)

**File locations (configurable):**

| Scope | Default Location |
|---|---|
| Workspace | `.github/instructions/` |
| User profile | `prompts/` folder of current VS Code profile |
| Claude compat | `.claude/rules/` (workspace), `~/.claude/rules/` (user) |

### 1c. Always-On Alternatives

- **`AGENTS.md`** — Cross-agent compatible (recognized by VS Code, Claude Code, etc.). Can be placed in workspace root or subfolders for monorepo support.
- **`CLAUDE.md`** — For compatibility with Claude Code. Searched in workspace root, `.claude/CLAUDE.md`, and `~/.claude/CLAUDE.md`.

### Instruction Priority

When multiple instruction types exist, all are provided to the model. Priority order:

1. **Personal instructions** (user-level) — highest
2. **Repository instructions** (`.github/copilot-instructions.md` or `AGENTS.md`)
3. **Organization instructions** — lowest

### Tips for Writing Instructions

- Keep each instruction short and self-contained
- Include reasoning ("Use X because Y is deprecated")
- Show code examples of preferred vs. avoided patterns
- Focus on non-obvious rules (skip what linters already enforce)
- Store project instructions in the workspace for version control

---

## 2. Agent Skills (Open Standard)

Agent Skills are the most powerful and portable customization mechanism. They are folders of instructions, scripts, and resources that Copilot loads **on demand** based on task relevance. Skills work across VS Code, Copilot CLI, and Copilot coding agent.

**Standard:** [agentskills.io](https://agentskills.io/) (originally developed by Anthropic, now an open standard)

### Skill Directory Structure

```
.github/skills/
└── my-skill/              # Directory name must match `name` field
    ├── SKILL.md           # Required: metadata + instructions
    ├── scripts/           # Optional: executable code
    ├── references/        # Optional: documentation
    └── assets/            # Optional: templates, resources
```

### SKILL.md Format

```markdown
---
name: my-skill
description: >
  Description of what the skill does and when to use it.
  Be specific about capabilities and use cases to help
  Copilot decide when to load automatically.
---

# Skill Instructions

## When to use this skill
Use when the user needs to...

## Step-by-step procedure
1. First do X...
2. Then do Y...

## Examples
- Input: ...
- Output: ...
```

### Frontmatter Fields

| Field | Required | Description |
|---|---|---|
| `name` | Yes | 1–64 chars. Lowercase, hyphens only. Must match parent directory name. |
| `description` | Yes | 1–1024 chars. What the skill does AND when to use it. |
| `argument-hint` | No | Hint text shown when invoked as slash command (e.g., `[test file] [options]`). |
| `user-invocable` | No | Default `true`. Set `false` to hide from `/` menu (still auto-loaded by agent). |
| `disable-model-invocation` | No | Default `false`. Set `true` to require manual `/` invocation only. |
| `license` | No | License name or reference to bundled license file. |
| `compatibility` | No | Environment requirements (packages, network access, etc.). |
| `metadata` | No | Arbitrary key-value map for additional properties. |
| `allowed-tools` | No | Space-delimited list of pre-approved tools. (Experimental.) |

### Skill Locations

| Type | Directories |
|---|---|
| **Project skills** (in repo) | `.github/skills/`, `.claude/skills/`, `.agents/skills/` |
| **Personal skills** (user) | `~/.copilot/skills/`, `~/.claude/skills/`, `~/.agents/skills/` |

Additional locations configurable via `chat.agentSkillsLocations` setting.

### How Copilot Uses Skills (Progressive Disclosure)

Skills use a 3-level loading system to keep context efficient:

1. **Level 1 — Discovery:** Copilot always reads `name` + `description` from all available skills (~100 tokens each). This is how it decides relevance.
2. **Level 2 — Instructions:** When a task matches, Copilot loads the full `SKILL.md` body (<5000 tokens recommended).
3. **Level 3 — Resources:** Copilot loads referenced files (scripts, examples, docs) only when it needs them.

This means you can install many skills without bloating context — only relevant content loads.

### Skills as Slash Commands

Skills automatically appear as `/` slash commands in chat (alongside prompt files). You can add context after the command:

```
/webapp-testing for the login page
/convert-paper 2401.12345
```

### Invocation Control

| `user-invocable` | `disable-model-invocation` | In `/` menu? | Auto-loaded? | Use case |
|---|---|---|---|---|
| (default) | (default) | Yes | Yes | General-purpose skills |
| `false` | (default) | No | Yes | Background knowledge |
| (default) | `true` | Yes | No | On-demand only |
| `false` | `true` | No | No | Disabled |

### Best Practices for Skills

- **Description is critical.** The `description` field is how Copilot decides whether to load the skill. Include specific keywords and use cases.
- Keep `SKILL.md` under 500 lines. Move detailed reference material to `references/`.
- Use relative paths from skill root to reference included files: `[guide](references/REFERENCE.md)`
- Keep file references one level deep — avoid deeply nested reference chains.
- Scripts should be self-contained with clear error messages.
- You can validate skills with the `skills-ref` CLI: `skills-ref validate ./my-skill`

### Generating Skills with AI

- Type `/create-skill` in VS Code chat and describe what you want
- Extract from conversation: "create a skill from how we just debugged that"

### Community Resources

- [github/awesome-copilot](https://github.com/github/awesome-copilot) — community collection
- [anthropics/skills](https://github.com/anthropics/skills) — reference skills
- Agent plugins can also bundle skills

---

## 3. Prompt Files (Slash Commands)

Prompt files are standalone Markdown files that encode common tasks as invokable `/` commands.

**Location:** `.github/prompts/` directory

```markdown
---
name: 'component'
description: 'Scaffold a new React component'
---
Create a new React component with the following structure:
- Functional component with TypeScript
- CSS module for styling
- Unit test file
- Storybook story

Component name: $input
```

- Invoke with `/component` in chat
- Best for single, repeatable tasks (scaffolding, running tests, PR prep)
- Generate with `/create-prompt` in chat

---

## 4. Reminding Copilot About Available Skills

A key question: how does Copilot know what capabilities are available?

### Automatic Discovery

- **`copilot-instructions.md`** and **`AGENTS.md`** are always loaded — their content is always in context.
- **`.instructions.md` files** are loaded when the `applyTo` glob matches active files, or when the description semantically matches the task.
- **Agent Skills** use progressive disclosure:
  - Copilot always reads all skill `name` + `description` fields at startup
  - A well-written description is all that's needed for Copilot to discover and activate skills

### Explicit Reminders in Instructions

You can reference skills and workflows in your `copilot-instructions.md` to nudge Copilot:

```markdown
## Available Skills

When working with research papers, use the `convert-paper` skill 
which handles downloading from arXiv and converting PDFs to Markdown.

When writing Python code, follow the conventions in the Python Standards 
instruction file (.github/instructions/python.instructions.md).
```

### Reminding via Chat

- Type `/skills` to open the Configure Skills menu and see all available skills
- Type `/instructions` to see all instruction files
- Type `/` to see all available slash commands (skills + prompt files)
- Check **Configure Chat (gear icon) > Chat Instructions** to verify which instructions are active
- Use **Configure Chat > Diagnostics** to troubleshoot loaded customizations

### Verification

To verify your customizations are being used:
- Expand the **References** section at the top of a chat response
- Check whether your instruction/skill files are listed
- If missing, check file locations, `applyTo` patterns, and relevant settings:
  - `chat.includeApplyingInstructions` (pattern-based instructions)
  - `chat.includeReferencedInstructions` (Markdown-linked instructions)
  - `chat.useAgentsMdFile` (AGENTS.md)

---

## 5. Choosing the Right Mechanism

| Scenario | Use |
|---|---|
| "Always use tabs, prefer arrow functions" | `copilot-instructions.md` |
| "Python files use Black formatter" | `.instructions.md` with `applyTo: "**/*.py"` |
| "Convert arXiv papers to Markdown" | Agent Skill (includes scripts, examples) |
| "Scaffold a React component" | Prompt file (`/component`) |
| "Database admin persona with SQL tools only" | Custom Agent |
| "Query our Jira board" | MCP Server |
| "Run prettier after every file edit" | Hook |

### When to Use Skills vs Instructions

| | Agent Skills | Custom Instructions |
|---|---|---|
| **Purpose** | Specialized capabilities & workflows | Coding standards & guidelines |
| **Portability** | Cross-agent (VS Code, CLI, coding agent, Cursor, Claude, etc.) | VS Code + GitHub.com |
| **Content** | Instructions + scripts + examples + resources | Instructions only |
| **Scope** | On-demand, loaded when relevant | Always-on or glob-matched |
| **Standard** | Open (agentskills.io) | VS Code-specific |

**Rule of thumb:** If it's a guideline → instruction. If it's a capability → skill.

---

## Sources

- [GitHub Docs: Adding repository custom instructions](https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot)
- [VS Code Docs: Customize AI overview](https://code.visualstudio.com/docs/copilot/copilot-customization)
- [VS Code Docs: Custom instructions](https://code.visualstudio.com/docs/copilot/customization/custom-instructions)
- [VS Code Docs: Agent Skills](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [Agent Skills Specification](https://agentskills.io/specification)
- [Agent Skills: What are skills?](https://agentskills.io/what-are-skills)
