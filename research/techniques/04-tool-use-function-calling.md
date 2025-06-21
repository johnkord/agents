# Tool Use and Function Calling: The Agent's Hands

> *Last updated: 2026-03-07*

## Why Tools Are Everything

An LLM without tools is a brain in a jar. Tools are what make agents _agentic_ — they transform language generation into real-world action. The quality of an agent's tool layer — how tools are designed, described, selected, and orchestrated — is one of the strongest predictors of agent success.

Anthropic revealed that when building their SWE-bench coding agent, they **"spent more time optimizing tools than the overall prompt."** This is the inversion most teams get wrong: they obsess over system prompts while neglecting tool design.

The concept to internalize: **Agent-Computer Interface (ACI)**. Just as decades of HCI research went into making software usable by humans, similar effort must go into making tools usable by agents. Tool descriptions, parameter names, error messages, output formats — this is the agent's UX. A well-designed ACI is a form of context engineering.

### Tools Are Structured Outputs

The 12-Factor Agents framework (Factor 4) makes a clarifying observation: **"Tool calls" are really just structured outputs.** When an LLM "calls a tool," it's actually just generating a JSON object that *your code* then interprets and executes. The LLM never directly executes anything — it produces data, and your deterministic code acts on it.

This framing is powerful because it means:
- You own the execution. You can validate, modify, or reject any tool call before executing it.
- You own the control flow. The loop is *your* code, not the LLM's.
- Tool calls and "structured output" / "JSON mode" are the same underlying mechanism — constrained generation.

## Anatomy of a Tool

Every tool exposed to an LLM has three components:

```json
{
  "name": "search_codebase",
  "description": "Search for code patterns across the workspace using regex or text matching. Returns matching file paths and line numbers with surrounding context.",
  "parameters": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "The search pattern. Use regex for flexible matching."
      },
      "file_pattern": {
        "type": "string",
        "description": "Glob pattern to filter files (e.g., '**/*.py'). Optional."
      },
      "max_results": {
        "type": "integer",
        "description": "Maximum results to return. Default: 20."
      }
    },
    "required": ["query"]
  }
}
```

**The name** tells the model _what_ the tool does (should be verb-noun: `search_codebase`, `read_file`, `create_issue`).

**The description** tells the model _when and how_ to use it. This is the most important part and is often under-invested in.

**The parameters** tell the model _what inputs_ are needed. Rich descriptions on each parameter dramatically reduce invocation errors.

## The Art of Tool Description Writing

Tool descriptions are a form of context engineering. They are instructions that the model interprets at every reasoning step. Great descriptions:

### Do:
- Explain when to use this tool vs. alternatives
- Describe expected output format
- Note common pitfalls and edge cases
- Include mini-examples in the description
- Specify what happens on failure

### Don't:
- Be vague ("Searches stuff")
- Duplicate information from parameter descriptions
- Assume the model knows your domain conventions
- Leave ambiguity about side effects

**Example of a mediocre vs. excellent tool description:**

```
# Mediocre
"description": "Executes a command in the terminal"

# Excellent  
"description": "Execute a shell command in a persistent bash terminal. 
The terminal preserves state (env vars, cwd) across calls. 
Use for: running tests, installing packages, file operations, git commands.
Do NOT use for: editing files (use edit_file instead), long-running servers 
(set isBackground=true for those).
Output is truncated at 60KB — use head/tail/grep to filter large outputs.
Commands run as the current user with their permissions."
```

The excellent description prevents 5+ common failure modes.

## Tool Design Principles

### 1. Single Responsibility
Each tool should do one thing well. Avoid Swiss Army knife tools that take a `mode` parameter. The model handles tool _selection_ better than tool _mode selection_.

```python
# Bad: One tool with modes
def file_tool(action: str, path: str, content: str = None):
    if action == "read": ...
    elif action == "write": ...
    elif action == "delete": ...

# Good: Separate tools with clear purposes
def read_file(path: str, start_line: int, end_line: int): ...
def write_file(path: str, content: str): ...
def delete_file(path: str): ...
```

### 2. Rich Error Messages
When a tool fails, the error message IS the model's only feedback. Make it actionable:

```python
# Bad
raise ToolError("File not found")

# Good
raise ToolError(
    f"File not found: {path}. "
    f"Working directory is {os.getcwd()}. "
    f"Similar files found: {find_similar(path)}. "
    f"Use list_directory to explore available files."
)
```

### 3. Idempotency Where Possible
Tools that can be safely retried on failure are much easier for agents to work with. If `create_file` fails halfway, can the agent call it again safely?

### 4. Output Schema Consistency
Tool outputs should be predictable. If `search` sometimes returns a list and sometimes a string, the model has to handle both — and often doesn't.

```python
# Consistent output schema
@dataclass
class SearchResult:
    matches: list[Match]
    total_count: int
    truncated: bool
    
    def to_context_string(self) -> str:
        """Format for LLM consumption — not JSON, but readable text"""
        lines = [f"Found {self.total_count} matches:"]
        for m in self.matches:
            lines.append(f"  {m.file}:{m.line}: {m.preview}")
        if self.truncated:
            lines.append(f"  ... and {self.total_count - len(self.matches)} more")
        return "\n".join(lines)
```

### 5. Tool Output Formatting for LLM Consumption
The output format matters enormously. Models parse structured text better than raw dumps:

```python
# Bad: Raw JSON dump
return json.dumps(api_response, indent=2)  # Wastes tokens, hard to parse

# Good: LLM-optimized format
return f"""API Response (Status: {response.status}):
- Users found: {len(response.users)}
- First user: {response.users[0].name} ({response.users[0].email})
- Query time: {response.query_time_ms}ms
Note: Full response has {len(response.users)} users. Request more if needed."""
```

### 6. Poka-Yoke Your Tools

[Poka-yoke](https://en.wikipedia.org/wiki/Poka-yoke) ("mistake-proofing") is a manufacturing concept: design things so they can't be used incorrectly. Apply this to tool parameters:

```python
# Mistake-prone: relative paths break when agent changes directory
def read_file(path: str): ...  # Agent might pass "./config.yaml" or "config.yaml"

# Poka-yoke'd: absolute paths always work regardless of cwd
def read_file(absolute_path: str): ...  # Force absolute paths
```

Anthropic discovered this exact issue building their SWE-bench agent: the model made mistakes with relative file paths after moving out of the root directory. Changing the tool to *require* absolute file paths eliminated the errors entirely.

Other poka-yoke strategies:
- Use enums instead of free-text for parameters with known valid values
- Default optional parameters to the safest option
- Validate inputs and return helpful errors *before* execution
- Make destructive operations require an explicit confirmation parameter

## Function Calling Mechanisms

### Native Function Calling (Preferred)
Modern LLM APIs (OpenAI, Anthropic, Google) support structured function calling:

```python
response = client.chat.completions.create(
    model="gpt-4",
    messages=messages,
    tools=[
        {
            "type": "function",
            "function": {
                "name": "search_codebase",
                "description": "...",
                "parameters": {...}
            }
        }
    ],
    tool_choice="auto"  # or "required" or {"type": "function", "function": {"name": "..."}}
)
```

**Advantages**: Structured output, validated parameters, parallel tool calls, model trained specifically on this format.

### Prompt-Based Tool Calling (Fallback)
For models without native function calling, tools can be described in the prompt:

```xml
<tools>
You have access to these tools:

search_codebase(query: str, file_pattern?: str) -> SearchResult
  Search for code patterns. Returns matching files and lines.

read_file(path: str, start_line: int, end_line: int) -> str  
  Read file contents. Line numbers are 1-indexed.

To use a tool, respond with:
<tool_call>
{"name": "tool_name", "arguments": {"param": "value"}}
</tool_call>
</tools>
```

**Advantages**: Works with any model. **Disadvantages**: More parsing errors, no parallel calls, wastes tokens on format instructions.

## Advanced Tool Patterns

### Dynamic Tool Loading

Don't present all 50 tools at once. Load tools dynamically based on task context:

```python
class DynamicToolRegistry:
    def __init__(self):
        self.all_tools = {}
        self.tool_categories = {}
    
    def get_tools_for_context(self, task_description: str, 
                                current_step: str,
                                recently_used: list[str]) -> list[Tool]:
        """Select relevant tools based on current context"""
        
        # Always include core tools
        tools = self.get_core_tools()  # read_file, search, etc.
        
        # Add task-specific tools
        task_category = classify_task(task_description)
        tools += self.tool_categories.get(task_category, [])
        
        # Keep recently used tools (model may need them again)
        for name in recently_used[-3:]:
            if name in self.all_tools:
                tools.append(self.all_tools[name])
        
        return deduplicate(tools)
```

### Tool Chains (Macros)

Common tool sequences can be bundled into higher-level operations:

```python
# Instead of: search → read_file → edit → read_file (verify)
# Offer a compound tool:
def find_and_replace(search_pattern: str, replacement: str, 
                     file_pattern: str = "**/*") -> str:
    """Find all occurrences of a pattern and replace them.
    Returns a summary of changes made."""
    matches = search(search_pattern, file_pattern)
    changes = []
    for match in matches:
        content = read_file(match.file)
        new_content = content.replace(search_pattern, replacement)
        write_file(match.file, new_content)
        changes.append(f"{match.file}: {match.count} replacements")
    return "\n".join(changes)
```

**Trade-off**: Compound tools reduce steps but reduce flexibility. Use them for common, well-defined sequences.

### Confirmation Gates

For destructive or irreversible operations, add confirmation:

```python
class ConfirmationGatedTool:
    def __init__(self, tool, requires_confirmation=True):
        self.tool = tool
        self.requires_confirmation = requires_confirmation
    
    def execute(self, **params):
        if self.requires_confirmation:
            # Return a preview instead of executing
            preview = self.tool.preview(**params)
            return ToolResult(
                status="awaiting_confirmation",
                preview=preview,
                message="This action requires confirmation. Review the preview and confirm to proceed."
            )
        return self.tool.execute(**params)
```

### Tool Result Caching

Avoid redundant tool calls for deterministic operations:

```python
class CachedToolExecutor:
    def __init__(self):
        self.cache = {}
    
    def execute(self, tool_name: str, params: dict):
        # Only cache read-only tools
        if tool_name in CACHEABLE_TOOLS:
            cache_key = (tool_name, frozenset(params.items()))
            if cache_key in self.cache:
                return f"[Cached] {self.cache[cache_key]}"
            result = execute_tool(tool_name, params)
            self.cache[cache_key] = result
            return result
        
        # Invalidate relevant cache entries for write tools
        if tool_name in WRITE_TOOLS:
            self.invalidate_related(tool_name, params)
        
        return execute_tool(tool_name, params)
```

## Parallel Tool Calls

Modern APIs support parallel tool calling — the model can request multiple tools simultaneously:

```json
{
  "tool_calls": [
    {"id": "call_1", "function": {"name": "read_file", "arguments": {"path": "src/main.py"}}},
    {"id": "call_2", "function": {"name": "read_file", "arguments": {"path": "src/utils.py"}}},
    {"id": "call_3", "function": {"name": "search", "arguments": {"query": "def process"}}}
  ]
}
```

**Design for parallelism:**
- Independent read operations should be parallelizable
- Write operations typically need sequencing
- The agent should be prompted to batch independent operations

## The Tool Explosion Problem

As agents gain capabilities, tool counts grow. 50+ tools is common in production agents. This creates problems:

1. **Token cost**: Tool descriptions consume context window
2. **Selection confusion**: More tools = more chances to pick the wrong one
3. **Description conflicts**: Similar tools with subtle differences

**Solutions:**

| Strategy | Description |
|---|---|
| **Hierarchical tools** | Group tools by category, only expand the relevant group |
| **Tool search** | Let the agent search for tools by description |
| **Progressive disclosure** | Start with core tools, unlock more as needed |
| **Tool descriptions in retrieval** | Store tool docs in a vector store, retrieve relevant ones |
| **Tool aliases** | Multiple names for the same tool to match different mental models |

## MCP (Model Context Protocol)

MCP is a standard (launched late 2024, adopted broadly through 2025-2026) for exposing tools to LLMs in a standardized way:

- **Servers** expose tools, resources, and prompts via a JSON-RPC protocol
- **Clients** (agent frameworks) connect to servers and present their capabilities to the model
- Enables **plug-and-play tool ecosystems** — any MCP server works with any MCP client

```
Agent Framework ←──MCP──→ GitHub Server
                ←──MCP──→ Database Server  
                ←──MCP──→ File System Server
                ←──MCP──→ Custom Business Logic Server
```

This is significant because it decouples tool implementation from agent implementation, enabling a marketplace of agent capabilities. Claude Code, VS Code Copilot, JetBrains, and many other tools now support MCP natively. The ecosystem has hundreds of community-built MCP servers for databases, APIs, cloud providers, and more.

**Practical advice**: If you're building tools for agents, consider exposing them as MCP servers from the start. It future-proofs your tools for any client that supports the protocol.

## Research-Backed Insights on Tool Use

### SWE-agent: Experimental Evidence for ACI Design

SWE-agent (Yang et al., 2024) provides the most rigorous experimental evidence for ACI design principles. Through ablation studies on SWE-bench:

| ACI Design Choice | Impact |
|---|---|
| **No line numbers** in file views | Agents can't reliably refer to code locations → edit failures |
| **Viewing entire files** | Context overflow, agent loses focus → performance drops |
| **No auto-lint after edits** | Agents accumulate syntax errors silently → cascading failures |
| **Raw terminal output** | Verbose, irrelevant formatting wastes tokens |
| **Window-based viewing** (100 lines) | Prevents context overflow → significant improvement |
| **Simplified search commands** | Agents don't need to remember grep flags → fewer errors |
| **Edit with explicit line ranges** | Prevents off-by-one errors → reliable editing |

Key result: Interface design had **more impact on performance** than prompt optimization. SWE-agent achieved 12.5% pass@1 on SWE-bench (SOTA at publication), primarily through ACI innovations.

### Toolformer: Self-Supervised Tool Learning

Toolformer (Schick et al., 2023) showed LMs can learn when and how to use tools through self-supervision. The key signal: "does inserting this tool call help predict subsequent tokens?" A 6.7B parameter model with tool access **matched or exceeded GPT-3 (175B)** on math, QA, and translation — foundational evidence that small models + good tools > large models without tools. This work directly anticipates native function calling in modern APIs.

### Gorilla: The API Hallucination Problem

Gorilla (Patil et al., 2023) documented that even GPT-4 hallucinates wrong API names, incorrect arguments, and invents non-existent endpoints. A fine-tuned LLaMA 7B + retrieval system outperformed GPT-4 on API calling benchmarks. Key takeaway: **tool descriptions are context** — providing documentation at inference time (retrieval-augmented tool use) beats memorized tool knowledge. This anticipates MCP's approach of providing tool docs on-demand.

### DynaSaur: Dynamic Action Creation

DynaSaur (Nguyen et al., 2024) challenges the assumption that agents must select from a fixed action set. Instead, agents **generate new actions as Python code** at each step, and successful actions are accumulated into a growing library:

- Agents can create novel tools to handle edge cases
- The action library grows over time, making the agent more capable
- Code generation IS action generation — the distinction between "tool use" and "code generation" dissolves

This is relevant because no predefined tool set can anticipate every situation. Production agents may benefit from a hybrid approach: predefined tools for common cases + code generation for novel situations.

---

*Next: [Memory Systems for Agents](../techniques/05-memory-systems.md)*
