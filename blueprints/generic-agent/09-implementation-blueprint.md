# Generic Agent Implementation Blueprint

> *Last updated: 2026-03-07*

## Overview

This is a practical blueprint for building a generic, extensible agent from scratch. Not tied to any specific framework — these are the building blocks you'd implement in any language.

### Design Philosophy: The Agent as Stateless Reducer

The 12-Factor Agents framework (Factor 12) advocates treating your agent as a **stateless reducer**: given the same context (messages, tool results, memory), it should produce the same output. All state lives in the context, not in hidden agent internals.

This means:
- The agent loop is a pure function: `(context) → (action | final_answer)`
- State is serializable — you can pause, persist, and resume at any point (Factor 6: Launch/Pause/Resume)
- Debugging is straightforward: reproduce any step by replaying the context
- Scaling is simple: no sticky sessions, no shared mutable state

The blueprint below follows this philosophy.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Agent Runtime                           │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                  Context Assembler                     │   │
│  │  ┌─────────┐ ┌──────────┐ ┌────────┐ ┌───────────┐  │   │
│  │  │ System   │ │ Retrieved│ │History │ │  Working   │  │   │
│  │  │ Prompt   │ │ Knowledge│ │Manager │ │  Memory    │  │   │
│  │  └─────────┘ └──────────┘ └────────┘ └───────────┘  │   │
│  └──────────────────────────────────────────────────────┘   │
│                           │                                  │
│                           ▼                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                   LLM Client                          │   │
│  │  Model selection • Retry logic • Token counting       │   │
│  └──────────────────────────────────────────────────────┘   │
│                           │                                  │
│                           ▼                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                  Tool Executor                         │   │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐      │   │
│  │  │FS    │ │Search│ │Shell │ │HTTP  │ │Custom│ ...   │   │
│  │  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘      │   │
│  └──────────────────────────────────────────────────────┘   │
│                           │                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │               Guardrails & Validation                  │   │
│  │  Step limits • Path restrictions • Output validation   │   │
│  └──────────────────────────────────────────────────────┘   │
│                           │                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │                  Event / Logging                       │   │
│  │  Structured logs • Metrics • Trajectory recording     │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. The Agent Loop

```python
"""
agent_loop.py - The core execution loop
"""
from dataclasses import dataclass, field
from typing import Any, Optional
import time
import json


@dataclass
class AgentConfig:
    model: str = "claude-sonnet-4-20250514"
    max_steps: int = 30
    max_tokens_per_turn: int = 4096
    temperature: float = 0.0
    system_prompt: str = ""
    tools: list = field(default_factory=list)
    

@dataclass
class StepResult:
    step_number: int
    thought: Optional[str]
    tool_calls: list[dict]
    tool_results: list[dict]
    tokens_used: int
    duration_ms: float
    

@dataclass
class AgentResult:
    success: bool
    output: str
    steps: list[StepResult]
    total_tokens: int
    total_duration_ms: float
    

class Agent:
    def __init__(self, config: AgentConfig, llm_client, tool_executor, 
                 context_assembler, guardrails=None, logger=None):
        self.config = config
        self.llm = llm_client
        self.tools = tool_executor
        self.context = context_assembler
        self.guardrails = guardrails
        self.logger = logger or default_logger
    
    async def run(self, user_message: str, 
                  session_context: dict = None) -> AgentResult:
        """Main agent execution loop"""
        steps = []
        total_tokens = 0
        start_time = time.time()
        
        # Initialize context
        self.context.initialize(
            system_prompt=self.config.system_prompt,
            user_message=user_message,
            session_context=session_context
        )
        
        for step_num in range(self.config.max_steps):
            step_start = time.time()
            
            # 1. Assemble context for this step
            messages = self.context.assemble()
            
            # 2. LLM inference
            response = await self.llm.generate(
                model=self.config.model,
                messages=messages,
                tools=self.config.tools,
                temperature=self.config.temperature,
                max_tokens=self.config.max_tokens_per_turn
            )
            
            total_tokens += response.usage.total_tokens
            
            # 3. Check for completion (no tool calls = done)
            if not response.tool_calls:
                return AgentResult(
                    success=True,
                    output=response.content,
                    steps=steps,
                    total_tokens=total_tokens,
                    total_duration_ms=(time.time() - start_time) * 1000
                )
            
            # 4. Execute tool calls
            tool_results = []
            for tool_call in response.tool_calls:
                # Guardrail check
                if self.guardrails:
                    allowed, reason = self.guardrails.check(
                        tool_call.name, tool_call.arguments
                    )
                    if not allowed:
                        tool_results.append({
                            "tool_call_id": tool_call.id,
                            "error": f"Blocked by guardrail: {reason}"
                        })
                        continue
                
                # Execute
                try:
                    result = await self.tools.execute(
                        tool_call.name, tool_call.arguments
                    )
                    tool_results.append({
                        "tool_call_id": tool_call.id,
                        "result": result
                    })
                except Exception as e:
                    tool_results.append({
                        "tool_call_id": tool_call.id,
                        "error": str(e)
                    })
            
            # 5. Update context with results
            self.context.add_assistant_message(response)
            self.context.add_tool_results(tool_results)
            
            # 6. Record step
            step = StepResult(
                step_number=step_num,
                thought=response.content,
                tool_calls=[tc.__dict__ for tc in response.tool_calls],
                tool_results=tool_results,
                tokens_used=response.usage.total_tokens,
                duration_ms=(time.time() - step_start) * 1000
            )
            steps.append(step)
            self.logger.log_step(step)
        
        # Max steps reached
        return AgentResult(
            success=False,
            output="Maximum steps reached without completing the task.",
            steps=steps,
            total_tokens=total_tokens,
            total_duration_ms=(time.time() - start_time) * 1000
        )
```

### 2. Context Assembler

```python
"""
context_assembler.py - Dynamic context construction
"""
from dataclasses import dataclass
import tiktoken


@dataclass
class ContextBudget:
    max_tokens: int = 128_000
    system_prompt_budget: int = 2_000
    instructions_budget: int = 3_000
    retrieved_knowledge_budget: int = 15_000
    history_budget: int = 20_000
    working_memory_budget: int = 3_000
    output_reserve: int = 4_096


class ContextAssembler:
    def __init__(self, budget: ContextBudget = None):
        self.budget = budget or ContextBudget()
        self.tokenizer = tiktoken.get_encoding("cl100k_base")
        
        # Context layers
        self.system_prompt: str = ""
        self.instructions: str = ""
        self.retrieved_knowledge: list[str] = []
        self.message_history: list[dict] = []
        self.working_memory: dict = {}
    
    def initialize(self, system_prompt: str, user_message: str, 
                   session_context: dict = None):
        self.system_prompt = system_prompt
        self.message_history = [{"role": "user", "content": user_message}]
        
        if session_context:
            self.working_memory = session_context
    
    def assemble(self) -> list[dict]:
        """Build the messages array for the LLM, respecting token budget"""
        messages = []
        
        # System prompt (always included, highest priority)
        system_content = self.system_prompt
        
        # Add working memory to system prompt if present
        if self.working_memory:
            wm_str = self._format_working_memory()
            if self._count_tokens(wm_str) <= self.budget.working_memory_budget:
                system_content += f"\n\n<working_memory>\n{wm_str}\n</working_memory>"
        
        # Add retrieved knowledge
        if self.retrieved_knowledge:
            knowledge_str = self._select_knowledge()
            system_content += f"\n\n<context>\n{knowledge_str}\n</context>"
        
        messages.append({"role": "system", "content": system_content})
        
        # Manage conversation history to fit budget
        history = self._compress_history()
        messages.extend(history)
        
        return messages
    
    def add_assistant_message(self, response):
        self.message_history.append({
            "role": "assistant",
            "content": response.content,
            "tool_calls": response.tool_calls
        })
    
    def add_tool_results(self, results: list[dict]):
        for result in results:
            self.message_history.append({
                "role": "tool",
                "tool_call_id": result["tool_call_id"],
                "content": result.get("result", result.get("error", ""))
            })
    
    def _compress_history(self) -> list[dict]:
        """Fit history into budget, summarizing older messages if needed"""
        budget = self.budget.history_budget
        
        # Start from most recent, work backward
        selected = []
        tokens_used = 0
        
        for msg in reversed(self.message_history):
            msg_tokens = self._count_tokens(str(msg.get("content", "")))
            
            if tokens_used + msg_tokens <= budget:
                selected.insert(0, msg)
                tokens_used += msg_tokens
            else:
                # If we can't fit more, add a summary of older messages
                remaining = self.message_history[:len(self.message_history) - len(selected)]
                if remaining:
                    summary = self._summarize_messages(remaining)
                    selected.insert(0, {
                        "role": "system",
                        "content": f"[Summary of earlier conversation: {summary}]"
                    })
                break
        
        return selected
    
    def _select_knowledge(self) -> str:
        """Select and truncate retrieved knowledge to fit budget"""
        budget = self.budget.retrieved_knowledge_budget
        selected = []
        tokens_used = 0
        
        for doc in self.retrieved_knowledge:
            doc_tokens = self._count_tokens(doc)
            if tokens_used + doc_tokens <= budget:
                selected.append(doc)
                tokens_used += doc_tokens
        
        return "\n---\n".join(selected)
    
    def _count_tokens(self, text: str) -> int:
        return len(self.tokenizer.encode(text))
    
    def _format_working_memory(self) -> str:
        lines = []
        for key, value in self.working_memory.items():
            lines.append(f"- {key}: {value}")
        return "\n".join(lines)
    
    def _summarize_messages(self, messages: list[dict]) -> str:
        """Placeholder — in production, use an LLM call to summarize"""
        return f"({len(messages)} earlier messages about the task)"
```

### 3. Tool Executor

```python
"""
tool_executor.py - Tool registration and execution
"""
from dataclasses import dataclass
from typing import Callable, Any
import json
import inspect


@dataclass
class ToolDefinition:
    name: str
    description: str
    parameters: dict  # JSON Schema
    handler: Callable
    is_destructive: bool = False
    cacheable: bool = False


class ToolExecutor:
    def __init__(self):
        self.tools: dict[str, ToolDefinition] = {}
        self.execution_log: list[dict] = []
        self.cache: dict[str, str] = {}
    
    def register(self, tool: ToolDefinition):
        self.tools[tool.name] = tool
    
    def register_function(self, func: Callable, description: str = None,
                          is_destructive: bool = False, cacheable: bool = False):
        """Auto-register a Python function as a tool"""
        name = func.__name__
        desc = description or func.__doc__ or f"Call {name}"
        params = self._infer_schema(func)
        
        self.tools[name] = ToolDefinition(
            name=name,
            description=desc,
            parameters=params,
            handler=func,
            is_destructive=is_destructive,
            cacheable=cacheable
        )
    
    async def execute(self, tool_name: str, arguments: dict) -> str:
        """Execute a tool and return its result as a string"""
        if tool_name not in self.tools:
            available = ", ".join(self.tools.keys())
            raise ToolNotFoundError(
                f"Tool '{tool_name}' not found. Available tools: {available}"
            )
        
        tool = self.tools[tool_name]
        
        # Check cache
        if tool.cacheable:
            cache_key = f"{tool_name}:{json.dumps(arguments, sort_keys=True)}"
            if cache_key in self.cache:
                return f"[Cached] {self.cache[cache_key]}"
        
        # Execute
        try:
            if inspect.iscoroutinefunction(tool.handler):
                result = await tool.handler(**arguments)
            else:
                result = tool.handler(**arguments)
            
            # Convert result to string for LLM consumption
            result_str = self._format_result(result)
            
            # Cache if applicable
            if tool.cacheable:
                self.cache[cache_key] = result_str
            
            # Log
            self.execution_log.append({
                "tool": tool_name,
                "arguments": arguments,
                "result": result_str[:500],  # Truncate for log
                "success": True
            })
            
            return result_str
            
        except Exception as e:
            self.execution_log.append({
                "tool": tool_name,
                "arguments": arguments,
                "error": str(e),
                "success": False
            })
            raise
    
    def get_tool_schemas(self) -> list[dict]:
        """Export tool schemas for LLM API"""
        return [
            {
                "type": "function",
                "function": {
                    "name": tool.name,
                    "description": tool.description,
                    "parameters": tool.parameters
                }
            }
            for tool in self.tools.values()
        ]
    
    def _infer_schema(self, func: Callable) -> dict:
        """Infer JSON Schema from function signature"""
        sig = inspect.signature(func)
        properties = {}
        required = []
        
        type_map = {
            str: "string",
            int: "integer",
            float: "number",
            bool: "boolean",
            list: "array",
            dict: "object"
        }
        
        for name, param in sig.parameters.items():
            annotation = param.annotation
            prop = {"type": type_map.get(annotation, "string")}
            
            if param.default == inspect.Parameter.empty:
                required.append(name)
            else:
                prop["default"] = param.default
            
            properties[name] = prop
        
        return {
            "type": "object",
            "properties": properties,
            "required": required
        }
    
    def _format_result(self, result: Any) -> str:
        if isinstance(result, str):
            return result
        if isinstance(result, (dict, list)):
            return json.dumps(result, indent=2, default=str)
        return str(result)
```

### 4. Guardrails

```python
"""
guardrails.py - Safety boundaries for agent execution
"""
from dataclasses import dataclass, field
import re


@dataclass
class GuardrailConfig:
    max_steps: int = 50
    max_file_size: int = 100_000  # characters
    forbidden_paths: list[str] = field(default_factory=lambda: [
        "/etc", "/sys", "/root", "/var/log",
        ".env", ".ssh", "credentials"
    ])
    allowed_commands: list[str] = field(default_factory=lambda: [
        "git", "npm", "npx", "python", "python3", "pip",
        "pytest", "node", "ls", "cat", "grep", "find",
        "head", "tail", "wc", "sort", "diff", "mkdir"
    ])
    max_command_length: int = 1000
    block_network_tools: bool = False


class Guardrails:
    def __init__(self, config: GuardrailConfig = None):
        self.config = config or GuardrailConfig()
        self.step_count = 0
        self.blocked_log = []
    
    def check(self, tool_name: str, arguments: dict) -> tuple[bool, str]:
        """Returns (allowed, reason)"""
        self.step_count += 1
        
        # Step limit
        if self.step_count > self.config.max_steps:
            return False, f"Maximum step limit ({self.config.max_steps}) exceeded"
        
        # Path-based checks
        path = arguments.get("path") or arguments.get("filePath") or ""
        if path:
            for forbidden in self.config.forbidden_paths:
                if forbidden in path:
                    self._log_block(tool_name, arguments, 
                                    f"Path contains forbidden segment: {forbidden}")
                    return False, f"Access to '{forbidden}' is not allowed"
        
        # Command execution checks
        if tool_name in ["run_command", "run_in_terminal", "execute"]:
            command = arguments.get("command", "")
            
            if len(command) > self.config.max_command_length:
                return False, "Command too long"
            
            base_cmd = command.strip().split()[0] if command.strip() else ""
            if base_cmd and base_cmd not in self.config.allowed_commands:
                # Check if it's a path to an allowed command
                base_name = base_cmd.split("/")[-1]
                if base_name not in self.config.allowed_commands:
                    return False, f"Command '{base_cmd}' not in allowed list"
            
            # Block dangerous patterns
            dangerous = [
                r"rm\s+-rf\s+/",       # rm -rf /
                r">\s*/dev/",           # redirect to /dev
                r"chmod\s+777",         # overly permissive
                r"curl.*\|\s*sh",       # pipe curl to shell
                r"wget.*\|\s*bash",     # pipe wget to bash
            ]
            for pattern in dangerous:
                if re.search(pattern, command):
                    return False, f"Dangerous command pattern detected"
        
        # File size checks
        if tool_name in ["write_file", "create_file"]:
            content = arguments.get("content", "")
            if len(content) > self.config.max_file_size:
                return False, f"File content exceeds max size ({self.config.max_file_size})"
        
        return True, "OK"
    
    def _log_block(self, tool_name, arguments, reason):
        self.blocked_log.append({
            "step": self.step_count,
            "tool": tool_name,
            "reason": reason
        })
```

### 5. Built-in Tools Library

```python
"""
tools_library.py - Common tools for a generic agent
"""
import os
import subprocess
import json
import glob as glob_module


# ─── File System Tools ─────────────────────────────────

def read_file(path: str, start_line: int = 1, end_line: int = -1) -> str:
    """Read the contents of a file. Lines are 1-indexed.
    If end_line is -1, reads to the end of file.
    Returns file contents with line numbers."""
    with open(path, 'r') as f:
        lines = f.readlines()
    
    if end_line == -1:
        end_line = len(lines)
    
    selected = lines[start_line - 1:end_line]
    numbered = [f"{i}: {line}" for i, line in 
                enumerate(selected, start=start_line)]
    return "".join(numbered)


def write_file(path: str, content: str) -> str:
    """Write content to a file, creating directories if needed."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w') as f:
        f.write(content)
    return f"Wrote {len(content)} characters to {path}"


def edit_file(path: str, old_string: str, new_string: str) -> str:
    """Replace an exact string in a file. The old_string must match exactly."""
    with open(path, 'r') as f:
        content = f.read()
    
    if old_string not in content:
        return f"ERROR: String not found in {path}. Make sure it matches exactly."
    
    count = content.count(old_string)
    if count > 1:
        return f"ERROR: String found {count} times. Include more context to match uniquely."
    
    new_content = content.replace(old_string, new_string, 1)
    with open(path, 'w') as f:
        f.write(new_content)
    
    return f"Successfully edited {path}"


def list_directory(path: str) -> str:
    """List files and folders in a directory."""
    entries = os.listdir(path)
    result = []
    for entry in sorted(entries):
        full = os.path.join(path, entry)
        suffix = "/" if os.path.isdir(full) else ""
        result.append(f"{entry}{suffix}")
    return "\n".join(result)


def search_files(pattern: str, directory: str = ".") -> str:
    """Search for files matching a glob pattern."""
    matches = glob_module.glob(pattern, root_dir=directory, recursive=True)
    if not matches:
        return f"No files matching '{pattern}' in {directory}"
    return "\n".join(sorted(matches)[:50])


# ─── Text Search Tools ─────────────────────────────────

def grep(pattern: str, path: str, is_regex: bool = False) -> str:
    """Search for text in files. Returns matching lines with file and line number."""
    cmd = ["grep", "-rn"]
    if not is_regex:
        cmd.append("-F")  # Fixed string matching
    cmd.extend([pattern, path])
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=30)
        output = result.stdout.strip()
        if not output:
            return f"No matches for '{pattern}' in {path}"
        
        lines = output.split("\n")
        if len(lines) > 50:
            return "\n".join(lines[:50]) + f"\n... and {len(lines) - 50} more matches"
        return output
    except subprocess.TimeoutExpired:
        return "Search timed out — try a more specific pattern or smaller directory"


# ─── Shell Execution ───────────────────────────────────

def run_command(command: str, timeout: int = 60) -> str:
    """Execute a shell command and return its output."""
    try:
        result = subprocess.run(
            command, shell=True, capture_output=True, text=True, 
            timeout=timeout, cwd=os.getcwd()
        )
        output = ""
        if result.stdout:
            output += result.stdout
        if result.stderr:
            output += f"\n[STDERR]: {result.stderr}"
        output += f"\n[Exit code: {result.returncode}]"
        
        # Truncate large outputs
        if len(output) > 10000:
            output = output[:5000] + "\n...(truncated)...\n" + output[-5000:]
        
        return output
    except subprocess.TimeoutExpired:
        return f"Command timed out after {timeout} seconds"
```

## Putting It All Together

```python
"""
main.py - Wire everything up and run
"""
import asyncio


async def create_generic_agent() -> Agent:
    """Factory function to create a fully configured generic agent"""
    
    # 1. Configure
    config = AgentConfig(
        model="claude-sonnet-4-20250514",
        max_steps=30,
        temperature=0.0,
        system_prompt=GENERIC_SYSTEM_PROMPT  # See below
    )
    
    # 2. Set up tools
    tools = ToolExecutor()
    tools.register_function(read_file, "Read file contents with line numbers", cacheable=True)
    tools.register_function(write_file, "Create or overwrite a file", is_destructive=True)
    tools.register_function(edit_file, "Edit a specific part of a file", is_destructive=True)
    tools.register_function(list_directory, "List directory contents", cacheable=True)
    tools.register_function(search_files, "Find files by glob pattern", cacheable=True)
    tools.register_function(grep, "Search for text patterns in files", cacheable=True)
    tools.register_function(run_command, "Execute shell commands", is_destructive=True)
    
    config.tools = tools.get_tool_schemas()
    
    # 3. Context assembler
    context = ContextAssembler(ContextBudget(max_tokens=128_000))
    
    # 4. Guardrails
    guardrails = Guardrails(GuardrailConfig(max_steps=30))
    
    # 5. LLM client (placeholder - implement with your API)
    llm = LLMClient(api_key=os.environ["API_KEY"])
    
    return Agent(config, llm, tools, context, guardrails)


GENERIC_SYSTEM_PROMPT = """You are a capable AI assistant that can use tools to accomplish tasks.

## How You Work
1. Analyze the user's request carefully
2. Plan your approach before acting
3. Use tools to gather information and make changes
4. Verify your work after making changes
5. Provide clear, concise responses

## Tool Use Principles
- Read before writing — understand the current state before modifying
- Make targeted changes — edit specific sections, don't rewrite entire files
- Verify after modifying — check that your changes work as intended
- Handle errors gracefully — if a tool call fails, diagnose and retry with corrections
- Batch independent read operations where possible

## When You're Uncertain
- If you don't know something, use search tools to find out
- If a task is ambiguous, state your interpretation and proceed (don't stall)
- If you've tried 2-3 approaches and failed, stop and ask for clarification
- If you can't complete a task with available tools, say so clearly

## Communication
- Be concise: answer in the minimum words needed
- Be specific: reference exact files, lines, and code
- Be honest: if something didn't work, say so

## Metacognition
Every 3-5 tool calls, briefly assess:
- Am I making progress toward the goal?
- Am I going in circles?
- Is there a simpler approach I should try?
"""


# Entry point
async def main():
    agent = await create_generic_agent()
    result = await agent.run("Find all TODO comments in the codebase and create a summary")
    print(result.output)
    print(f"Completed in {len(result.steps)} steps, {result.total_tokens} tokens")

if __name__ == "__main__":
    asyncio.run(main())
```

## Extending the Generic Agent

### For a Coding Agent
Add: test runner, linter, git tools, language server integration, CLAUDE.md-style project memory files

### For a Research Agent
Add: web search, URL fetcher, PDF parser, citation manager, note-taking tools

### For a Data Agent
Add: SQL executor, DataFrame tools, chart generator, data profiler

### For an Orchestrator
Add: sub-agent spawning, task decomposition, result synthesis, shared blackboard

The generic agent is the foundation. Specialization is achieved through tools, system prompts, and memory configuration — not architectural changes.

### Key Principle: Own Your Stack

The 12-Factor Agents project found that most successful production agents are built *without* heavy frameworks. Frameworks help with prototyping, but production demands that you understand every line of your agent loop, every tool description, and every context assembly step. The blueprint above is deliberately minimal — you should be able to read, understand, and modify every component.

---

*Next: [Language Selection for Agents](../../research/techniques/10-language-selection-for-agents.md)* · *Back to [Index](../../README.md)*
