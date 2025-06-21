#!/usr/bin/env python3
"""Download arXiv papers as PDFs and convert to Markdown using Docling."""

import os
import sys
import time
import subprocess
from pathlib import Path

# Paper name -> arXiv ID mapping
PAPERS = {
    "adas-automated-design-2024": "2408.08435",
    "agent-as-a-judge-2024": "2410.10934",
    "agent-protocols-survey-2025": "2504.16736",
    "agentbench-evaluating-llms-2023": "2308.03688",
    "agentboard-evaluation-2024": "2401.13178",
    "agentic-rag-survey-2025": "2501.09136",
    "autogen-multi-agent-conversation-2023": "2308.08155",
    "autonomous-agents-survey-wang-2023": "2308.11432",
    "brittle-react-prompting-2024": "2405.13966",
    "chain-of-thought-prompting-2022": "2201.11903",
    "cognitive-architectures-coala-2023": "2309.02427",
    "dspy-declarative-lm-pipelines-2023": "2310.03714",
    "dynasaur-dynamic-actions-2024": "2411.01747",
    "generative-agents-stanford-2023": "2304.03442",
    "gorilla-api-calling-2023": "2305.15334",
    "lats-tree-search-2023": "2310.04406",
    "llm-agents-survey-2023": "2309.07864",
    "memgpt-llm-operating-system-2023": "2310.08560",
    "metagpt-multi-agent-sop-2023": "2308.00352",
    "mixture-of-agents-2024": "2406.04692",
    "openhands-software-agents-2024": "2407.16741",
    "osworld-computer-benchmark-2024": "2404.07972",
    "react-reasoning-acting-2022": "2210.03629",
    "reflexion-verbal-reinforcement-2023": "2303.11366",
    "self-discover-reasoning-structures-2024": "2402.03620",
    "swe-agent-aci-2024": "2405.15793",
    "swe-bench-github-issues-2023": "2310.06770",
    "tau-bench-tool-agent-user-2024": "2406.12045",
    "toolformer-self-taught-tools-2023": "2302.04761",
    "tree-of-thoughts-2023": "2305.10601",
    "voyager-lifelong-learning-2023": "2305.16291",
    # --- Recent papers (Dec 2025 – Mar 2026): context engineering & agent design ---
    "monadic-context-engineering-2025": "2512.22431",
    "structured-context-engineering-2026": "2602.05447",
    "contextual-memory-virtualisation-2026": "2602.22402",
    "neural-paging-context-management-2026": "2603.02228",
    "swe-pruner-context-pruning-2026": "2601.16746",
    "pensieve-stateful-context-2026": "2602.12108",
    "long-context-reasoning-limits-2026": "2602.16069",
    "contextcov-agent-constraints-2026": "2603.00822",
    "architecting-agentos-2026": "2602.20934",
    "auton-agentic-framework-2026": "2602.23720",
    "formalizing-agent-designs-2026": "2602.08276",
    "theory-of-code-space-2026": "2603.00601",
    "mcp-design-choices-2026": "2602.15945",
    "aeon-memory-management-2026": "2601.15311",
    "anatomy-agentic-memory-2026": "2602.19320",
    "evaluating-memory-structure-2026": "2602.11243",
    "adaptive-memory-admission-2026": "2603.04549",
    "caster-multi-agent-routing-2026": "2601.19793",
    "adaptive-scalable-agent-coordination-2026": "2602.08009",
    "data-engineering-terminal-agents-2026": "2602.21193",
    # --- Batch 3 (Mar 2026): additional cutting-edge papers ---
    # Context & Memory
    "hippocampus-memory-module-2026": "2602.13594",
    "ariadnemem-lifelong-memory-2026": "2603.03290",
    "memexrl-indexed-experience-2026": "2603.04257",
    # Agent Reasoning & Planning
    "agentic-reasoning-survey-2026": "2601.12538",
    "himac-hierarchical-long-horizon-2026": "2603.00977",
    "think-fast-slow-cognitive-depth-2026": "2602.12662",
    "structuredagent-andor-planning-2026": "2603.05294",
    # Agent Training (RL)
    "mage-meta-rl-exploration-2026": "2603.03680",
    "tool-r0-self-evolving-agents-2026": "2602.21320",
    "exploratory-memory-augmented-rl-2026": "2602.23008",
    # MCP & Agent Protocols
    "mcp-server-description-smells-2026": "2602.18914",
    "mcp-atlas-benchmark-2026": "2602.00933",
    "mcp-information-fidelity-2026": "2602.13320",
    "agent-protocol-security-comparison-2026": "2602.11327",
    # Agent Architecture
    "caveagent-stateful-runtime-2026": "2601.01569",
    "agent-skills-architecture-2026": "2602.12430",
    "hyfunc-agentic-function-calls-2026": "2602.13665",
    # Multi-Agent & Evaluation
    "dr-mas-stable-rl-multi-agent-2026": "2602.08847",
    "managing-uncertainty-multi-agent-2026": "2602.23005",
    "gaia2-async-agents-benchmark-2026": "2602.11964",
    # --- Batch 4 (Mar 2026): KV cache, agent safety, protocols, code agents, test-time scaling ---
    # KV Cache / Context Compression
    "kvzip-kv-cache-compression-2025": "2505.23416",          # NeurIPS 2025 Oral: query-agnostic KV cache compression
    "active-context-compression-2026": "2601.07190",          # Autonomous memory management in LLM agents
    "crystal-kv-cot-cache-2026": "2601.16986",               # KV cache optimized for chain-of-thought reasoning
    "learning-to-evict-kv-cache-2026": "2602.10238",          # Learning-based KV cache eviction policy
    "hold-onto-thought-kv-reasoning-2025": "2512.12008",      # Assessing KV cache compression impact on reasoning
    # Agent Safety / Prompt Injection Defense
    "llamafirewall-agent-guardrails-2025": "2505.03574",      # Meta's open-source guardrail system
    "agentsentry-prompt-injection-defense-2026": "2602.22724", # Temporal causal diagnostics for PI defense
    "icon-prompt-injection-defense-2026": "2602.20708",        # Inference-time correction for PI defense
    "agentsys-secure-memory-management-2026": "2602.07398",    # Secure agents via hierarchical memory management
    "attention-defense-prompt-injection-2025": "2512.08417",   # NDSS 2026: attention-based PI defense
    "securing-mcp-tool-poisoning-2025": "2512.06556",          # Defending MCP against tool poisoning attacks
    "vigil-tool-stream-injection-2026": "2601.05755",          # Verify-before-commit tool stream defense
    # Agent Protocols / Interoperability
    "agent-interoperability-protocols-survey-2025": "2505.02279",  # Survey of MCP, ACP, A2A, ANP
    "coral-agent-to-agent-communication-2026": "2601.09883",       # Information-flow orchestrated A2A paradigm
    "agentic-ai-frameworks-protocols-2025": "2508.10146",          # Architectures, protocols, design challenges
    # Test-Time Compute Scaling
    "art-of-scaling-test-time-compute-2025": "2512.02008",     # Comprehensive survey on test-time scaling for LLMs
    "agentark-distill-multi-agent-2026": "2602.03955",         # Distilling multi-agent intelligence into single agent
    # Code Agents / Software Engineering
    "swe-adept-deep-codebase-analysis-2026": "2603.01327",     # Agentic framework for structured issue resolution
    "repo-intelligence-graph-2026": "2601.10112",              # Deterministic architectural map for code assistants
    "davinci-dev-agent-midtraining-2026": "2601.18418",        # Agent-native mid-training for software engineering
    # --- Batch 5: Deep Research Agents, Self-Reflection Loops, Verification Patterns ---
    # Deep Research Agent Design
    "deep-verifier-self-evolving-2026": "2601.15808",              # Self-Evolving Deep Research Agents via Test-Time Rubric-Guided Verification
    "deep-research-survey-2025": "2512.02038",                     # Deep Research: A Systematic Survey (3-stage roadmap, 4 components)
    "step-deep-research-2025": "2512.20491",                       # Step-DeepResearch: progressive training with Checklist-style Judger
    "useful-deep-research-agents-2025": "2512.01948",              # FINDER benchmark + DEFT failure taxonomy for deep research agents
    "rl-deep-research-design-2025": "2510.15862",                  # Rethinking the Design of RL-Based Deep Research Agents (SOTA 7B)
    "sfr-deep-research-rl-2025": "2509.06283",                     # SFR-DeepResearch: RL for Autonomously Reasoning Single Agents
    "deep-planner-advantage-shaping-2025": "2510.12979",           # DeepPlanner: Scaling Planning for Deep Research Agents via RL
    "researstudio-controllable-agents-2025": "2510.12194",         # ResearStudio: Human-Intervenable Deep-Research Agents (EMNLP 2025 Oral)
    # Self-Reflection & Self-Correction
    "reflection-driven-control-2025": "2512.21354",                # Reflection-Driven Control for Trustworthy Code Agents (AAAI 2026 Workshop)
    "structured-reflection-tool-use-2025": "2509.18847",           # Failure Makes the Agent Stronger: Structured Reflection for Tools
    "re-searcher-self-reflection-2025": "2509.26048",              # RE-Searcher: Robust Agentic Search with Self-reflection
    "spontaneous-self-correction-2025": "2506.06923",              # SPOC: Spontaneous Self-Correction via interleaved verify-in-generation
    "dyna-think-world-model-2025": "2506.00320",                   # Dyna-Think: Reasoning + Acting + World Model Simulation
    # Verification & Test-Time Self-Refinement
    "corefine-self-refinement-2026": "2602.08948",                 # CoRefine: Confidence-Guided Self-Refinement for Test-Time Compute
    "mas2-self-rectifying-2025": "2509.24323",                     # MAS²: Self-Generative, Self-Configuring, Self-Rectifying MAS
    # --- Batch 6: Long-Running / Life Augmentation / Proactive Agents ---
    # Proactive Agents (Core)
    "proactive-agent-reactive-to-active-2024": "2410.12361",       # Proactive Agent: Shifting LLM Agents from Reactive to Active Assistance
    "ask-before-plan-proactive-2024": "2406.12639",                # Ask-before-Plan: Proactive Language Agents for Real-World Planning (EMNLP 2024)
    "contextagent-proactive-sensory-2025": "2505.14668",           # ContextAgent: Context-Aware Proactive LLM Agents (NeurIPS 2025)
    "proagent-sensory-contexts-2025": "2512.06721",                # ProAgent: Harnessing On-Demand Sensory Contexts for Proactive Systems
    "propersim-proactive-personalized-2025": "2509.21730",         # ProPerSim: Proactive and Personalized AI Assistants (ICLR 2026)
    "bao-proactive-agentic-optimization-2026": "2602.11351",       # BAO: Behavioral Agentic Optimization for Proactive Agents
    "intentrl-proactive-deep-research-2026": "2602.03468",         # IntentRL: Training Proactive User-Intent Agents via RL
    "proagentbench-proactive-eval-2026": "2602.04482",             # ProAgentBench: Evaluating LLM Agents for Proactive Assistance
    "proper-proactivity-benchmark-2026": "2601.09926",             # PROPER: Proactivity Benchmarking & Knowledge Gap Navigation
    # Personalized & Long-Horizon Agents
    "o-mem-personalized-self-evolving-2025": "2511.13593",         # O-Mem: Omni Memory for Personalized, Long Horizon, Self-Evolving Agents
    "personalized-llm-agents-survey-2026": "2602.22680",           # Toward Personalized LLM-Powered Agents: Survey
    "amemgym-long-horizon-memory-2026": "2603.01966",              # AMemGym: Interactive Memory Benchmarking for Long-Horizon Conversations (ICLR 2026)
    "promemassist-proactive-wearable-2025": "2507.21378",          # ProMemAssist: Proactive Assistance via Working Memory Modeling (UIST'25)
    # Always-On / Life Augmentation Agents
    "egocentric-copilot-smart-glasses-2026": "2603.01104",         # Egocentric Co-Pilot: Always-On Web Agents for Smart Glasses (WWW 2026)
    "next-paradigm-user-centric-2026": "2602.15682",               # The Next Paradigm Is User-Centric Agent, Not Platform-Centric Service
    "choose-agent-advisors-delegates-2026": "2602.12089",          # Choose Your Agent: Tradeoffs in AI Advisors, Coaches, and Delegates
    # Long-Running Agent Infrastructure
    "esaa-event-sourcing-agents-2026": "2602.23193",               # ESAA: Event Sourcing for Autonomous Agents
    "alignment-in-time-long-horizon-2026": "2602.17910",           # Alignment in Time: Peak-Aware Orchestration for Long-Horizon Systems
    "internet-agentic-ai-distributed-2026": "2602.03145",          # Internet of Agentic AI: Incentive-Compatible Distributed Teaming
    "declarative-agent-workflows-2025": "2512.19769",              # A Declarative Language for Building and Orchestrating Agent Workflows
}

BASE_DIR = Path(__file__).resolve().parent.parent / "papers"
PDF_DIR = BASE_DIR / "pdfs"
DOCLING_DIR = BASE_DIR / "docling"


def download_pdf(name: str, arxiv_id: str) -> Path:
    """Download PDF from arXiv if not already present."""
    pdf_path = PDF_DIR / f"{name}.pdf"
    if pdf_path.exists():
        print(f"  [skip] PDF already exists: {pdf_path.name}")
        return pdf_path

    url = f"https://arxiv.org/pdf/{arxiv_id}"
    print(f"  [download] {url}")
    result = subprocess.run(
        ["curl", "-L", "-o", str(pdf_path), url],
        capture_output=True, text=True, timeout=60
    )
    if result.returncode != 0 or not pdf_path.exists():
        print(f"  [ERROR] Failed to download {name}: {result.stderr}")
        return None

    size_mb = pdf_path.stat().st_size / (1024 * 1024)
    print(f"  [ok] Downloaded {size_mb:.1f} MB")
    return pdf_path


def convert_with_docling(name: str, pdf_path: Path) -> Path:
    """Convert PDF to Markdown using Docling."""
    md_path = DOCLING_DIR / f"{name}.md"
    if md_path.exists():
        print(f"  [skip] Docling MD already exists: {md_path.name}")
        return md_path

    print(f"  [convert] Running Docling on {pdf_path.name}...")
    start = time.time()

    try:
        from docling.document_converter import DocumentConverter
        converter = DocumentConverter()
        result = converter.convert(str(pdf_path))
        md_text = result.document.export_to_markdown()
        elapsed = time.time() - start

        with open(md_path, "w") as f:
            f.write(md_text)

        print(f"  [ok] Converted in {elapsed:.1f}s ({len(md_text)} chars)")
        return md_path

    except Exception as e:
        elapsed = time.time() - start
        print(f"  [ERROR] Docling failed after {elapsed:.1f}s: {e}")
        return None


def main():
    PDF_DIR.mkdir(parents=True, exist_ok=True)
    DOCLING_DIR.mkdir(parents=True, exist_ok=True)

    # Allow filtering by paper name prefix
    filter_prefix = sys.argv[1] if len(sys.argv) > 1 else None

    papers = PAPERS
    if filter_prefix:
        papers = {k: v for k, v in PAPERS.items() if k.startswith(filter_prefix)}
        print(f"Filtering to {len(papers)} papers matching '{filter_prefix}'")

    total = len(papers)
    success_dl = 0
    success_cv = 0
    failures = []

    print(f"\n{'='*60}")
    print(f"Processing {total} papers")
    print(f"PDFs -> {PDF_DIR}")
    print(f"Markdown -> {DOCLING_DIR}")
    print(f"{'='*60}\n")

    for i, (name, arxiv_id) in enumerate(papers.items(), 1):
        print(f"[{i}/{total}] {name} (arXiv:{arxiv_id})")

        # Download
        pdf_path = download_pdf(name, arxiv_id)
        if pdf_path:
            success_dl += 1
        else:
            failures.append((name, "download"))
            continue

        # Convert
        md_path = convert_with_docling(name, pdf_path)
        if md_path:
            success_cv += 1
        else:
            failures.append((name, "convert"))

        print()

    print(f"\n{'='*60}")
    print(f"SUMMARY: {success_dl}/{total} downloaded, {success_cv}/{total} converted")
    if failures:
        print(f"\nFailures:")
        for name, stage in failures:
            print(f"  - {name} ({stage})")
    print(f"{'='*60}")


if __name__ == "__main__":
    main()
