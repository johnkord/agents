# Agent Engineering Knowledge Base

This repository collects research and knowledge about building AI agents, including architecture patterns, context engineering, tool use, memory, multi-agent systems, evaluation, and implementation.

## Project Structure

- `knowledge-base/` — Curated research documents on agent engineering topics
- `papers/pdfs/` — Source PDFs downloaded from arXiv
- `papers/docling/` — Faithful Markdown conversions of PDFs (machine-extracted via Docling)
- `scripts/convert_papers.py` — Automated download + conversion pipeline
- `research/` — Research notes and tool comparisons
- `blueprints/` — Implementation blueprints

## Key Rules

- **Never synthesize paper content from memory.** All paper Markdown in `papers/docling/` must be machine-extracted from actual PDFs using Docling.
- When adding new research papers, use the `convert-paper` skill which handles the full arXiv download and Docling conversion pipeline.
- When diagnosing or troubleshooting the Research Agent blueprint, use the `research-agent-investigation` skill for diagnostic methodology, MAF API pitfalls, and log analysis patterns.
- When analyzing Forge coding agent session logs for improvements, use the `forge-improve` skill. It guides a critical review of the full session timeline, tool usage, failure patterns, and produces actionable recommendations.
- Python dependencies are managed in `.venv/`. Activate with `source .venv/bin/activate`.
