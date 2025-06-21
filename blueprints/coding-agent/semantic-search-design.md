# SemanticSearch Tool — Design Document

> March 2026 — Feature design for Forge's semantic code search capability
> Grounded in 8 papers from our collection + 4 open-source projects

## 1. Problem Statement

Forge's `grep_search` finds exact text; `file_search` finds files by name. Neither can answer:
- "Where is authentication handled?"
- "Find the error recovery logic"
- "Which class manages the database connection?"

These queries require **semantic understanding** — matching by meaning, not literal text. Current agents (including Forge) compensate by running multiple grep searches with guessed keywords, wasting 3-5 steps and 10-20K tokens per semantic lookup.

## 2. Research Findings

### From Our Paper Collection (8 papers)

**Retrieval Architecture:**

| Paper | Key Finding | Relevance |
|-------|------------|-----------|
| Agentic RAG Survey (2025) | Hybrid BM25 + dense vectors beats either alone. Agent-driven iterative retrieval outperforms single-shot. Route queries by complexity. | Core architecture recommendation. |
| HyFunc (2026) | Soft-token embedding retrieval finds the right tool from a large library in ~104ms via cosine similarity against pre-computed embeddings. A cascade (big model for intent → small model + retriever) achieves competitive accuracy at 0.828s. | Demonstrates embedding retrieval is fast enough for interactive use. |
| MCP-Atlas (2026) | Tool discovery (≈ code discovery) is the #1 failure mode — 57% of failures are "wrong tool selected." Distractors destroy naive text matching. | Validates that better search is the highest-leverage improvement. |

**Repository Understanding:**

| Paper | Key Finding | Relevance |
|-------|------------|-----------|
| RIG (2026) | Pre-computed structural map (build targets, deps, tests) improved accuracy 12.2% and cut time 54%. Build topology > code-level AST graphs. | Structural pre-computation is high leverage and zero-ML. |
| SWE-Adept (2026) | Agent-directed DFS over dependency trees with tree-sitter indexing is the practical sweet spot. Return skeletons first, full code only for shortlisted candidates. | Tree-sitter is the right indexing primitive. |

**Context Management:**

| Paper | Key Finding | Relevance |
|-------|------------|-----------|
| SWE-Pruner (2026) | 76% of agent tokens go to read operations. A 0.6B neural skimmer with goal-aware line pruning: -23-54% tokens while *improving* accuracy. | Validates that the retrieval→read→prune pipeline matters more than retrieval quality alone. |
| CMV (2026) | Structural trimming (strip raw outputs, keep synthesis) gets 20-40% reduction with zero information loss. The expensive part isn't finding code — it's re-deriving the mental model. | Search results should include structural context (what the code does) not just locations. |
| Neural Paging (2026) | Treat context as semantic cache, not log. Fixed-window paging: O(N·K²) vs O(N²). For code tasks, β ≤ 0.10 — structured tasks are forgiving for eviction. | Validates our sawtooth approach. Search should populate a "working set" of relevant code blocks. |

### From Open-Source Projects

| Project | Approach | Status |
|---------|----------|--------|
| **Zoekt** (Sourcegraph) | Trigram indexing + BM25 ranking + symbol awareness. Written in Go. Used by Sourcegraph at scale. | Active. Heavy dependency (Go binary). |
| **Bloop** | Tantivy (Rust BM25) + Qdrant (vector DB) + Tree-sitter + on-device embeddings. | Archived Jan 2025. Architecture is reference-quality. |
| **Microsoft CodeBERT** family | CodeBERT, GraphCodeBERT, UniXcoder — pretrained code+NL models. UniXcoder supports code search via cross-modal embeddings. | Mature. Python/PyTorch. Heavy for local .NET use. |
| **Jina Code Embeddings** | `jina-embeddings-v2-base-code` (200M params), `jina-code-embeddings-1.5b` (2B params). Available on HuggingFace. ONNX export possible. 380K+ downloads. | Active. Best candidate for local embedding if we go that route. |

## 3. Design Options (Evaluated)

### Option A: Multi-Keyword Expansion (Zero Infrastructure)

**How:** Decompose the semantic query into keywords using simple NLP (split on spaces, add synonyms from a small dictionary). Run multiple `grep_search` calls, union and rank by overlap.

```
Query: "where is authentication handled?"
→ grep "auth" + grep "login" + grep "authenticate" + grep "credential" + grep "session"
→ Rank: files matching 3+ keywords > files matching 1
```

| Metric | Value |
|--------|-------|
| **Accuracy** | Low — no true semantic understanding. "retry logic" won't find "backoff strategy" |
| **Latency** | ~50ms (multiple greps) |
| **Index time** | 0 |
| **Dependencies** | None |
| **Implementation** | ~50 lines of C# |

**Verdict:** Good enough for v1. Ship fast, iterate later.

### Option B: Tree-Sitter Definition Index + BM25

**How:** Parse all code files with tree-sitter to extract definitions (classes, functions, methods) with their names, docstrings, parameter lists, and file locations. Build an in-memory inverted index. Search using BM25-style TF-IDF scoring.

```
Index entry:
{
  type: "method",
  name: "CheckToolCall",
  class: "Guardrails",
  file: "Guardrails.cs:27",
  summary: "Check whether a tool call is allowed. Returns (allowed, reason).",
  params: ["toolName", "arguments"]
}

Query: "where are safety checks for tool calls?"
→ BM25("safety check tool call") → ranks CheckToolCall highest
```

| Metric | Value |
|--------|-------|
| **Accuracy** | Medium — catches identifier names and docstrings. Misses semantic similarity across synonyms. |
| **Latency** | ~5ms per query (in-memory index) |
| **Index time** | ~2-5s for 10K files (tree-sitter is fast) |
| **Dependencies** | Tree-sitter CLI or .NET binding |
| **Implementation** | ~300-500 lines of C# + grammar files |

**Verdict:** Strong v2. Language-aware, catches function boundaries, fast. The research (SWE-Adept, RIG) validates this approach.

### Option C: Local Code Embedding Model (ONNX)

**How:** Use a quantized code embedding model (e.g., Jina Code Embeddings 0.5B or UniXcoder) via ONNX Runtime. Embed code chunks at index time, embed queries at search time, cosine similarity for retrieval.

| Metric | Value |
|--------|-------|
| **Accuracy** | High — true semantic similarity. "auth" matches "login" matches "credential" |
| **Latency** | ~50-200ms per query (CPU inference) |
| **Index time** | ~30s-2min for 10K files (embedding each chunk) |
| **Dependencies** | Microsoft.ML.OnnxRuntime (~50MB), model file (~200MB-2GB) |
| **Implementation** | ~400-600 lines + model integration |

**Candidates:**
- `jinaai/jina-code-embeddings-0.5b` — 500M params, 34.9K downloads, code-specific
- `jinaai/jina-embeddings-v2-base-code` — 200M params, 380K downloads, multilingual + code
- `Salesforce/codet5p-110m-embedding` — 110M params, 143K downloads, lightweight
- `microsoft/unixcoder-base` — 125M params, from CodeBERT family

**Verdict:** Best accuracy but heaviest dependency. Phase 4+ investment.

### Option D: Hybrid BM25 + Embedding Re-ranking

**How:** Combine Options B and C. BM25 for fast recall (top 50), embeddings for precision re-ranking (top 10). This is the research-recommended approach (Agentic RAG Survey).

| Metric | Value |
|--------|-------|
| **Accuracy** | Highest — BM25 catches exact matches, embeddings catch semantic matches |
| **Latency** | ~100-250ms (BM25 is instant, embedding re-ranks top-50) |
| **Index time** | Combines B and C |
| **Dependencies** | Tree-sitter + ONNX Runtime + model |
| **Implementation** | ~600-800 lines |

**Verdict:** The destination. Build toward this iteratively.

### Option E: LLM-as-Retriever (Use the agent itself)

**How:** Instead of a separate retrieval system, have the agent call `grep_search` and `file_search` iteratively, using its own reasoning to refine queries. The Agentic RAG Survey found this "retrieve → evaluate → re-retrieve" loop outperforms single-shot retrieval.

```
Agent: grep_search("auth") → 50 results, too many
Agent: grep_search("auth middleware") → 3 results, found it
```

| Metric | Value |
|--------|-------|
| **Accuracy** | High — the LLM understands context and can refine |
| **Latency** | ~3-5s per search (multiple LLM turns) |
| **Index time** | 0 |
| **Dependencies** | None — uses existing tools |
| **Implementation** | System prompt guidance only |
| **Cost** | 5-15K tokens per semantic lookup |

**Verdict:** This is what Forge already does. The question is whether we can make it cheaper.

## 4. Critical Analysis

### What actually matters for a coding agent?

The research points to a surprising conclusion: **the retrieval method matters less than the retrieval pipeline.**

- HyFunc showed that a simple MLP retriever with pre-computed embeddings matches much more expensive approaches
- SWE-Adept showed that agent-directed search (DFS following dependencies) beats any static index
- Agentic RAG showed that iterative refinement beats single-shot, regardless of the underlying retriever
- Neural Paging showed that context management (what to keep in working memory) matters more than retrieval accuracy

This suggests Option E (LLM-as-retriever) is actually a strong baseline — the agent's ability to reason about and refine its searches compensates for the lack of semantic indexing. The cost, though, is high in tokens.

### The real bottleneck

From our own Forge data:
- Steps 0-6 of every task are navigation/exploration (~30% of total tokens)
- The agent re-reads files it already read because compression erased the content
- `file_search` being a stub forced fallback to slower alternatives

**The highest-leverage improvements are NOT better semantic search. They are:**
1. **REPO.md** (RIG-style structural map) — eliminates "where is X?" questions entirely
2. **Better compression summaries** — include file content digests so the agent doesn't re-read
3. **FileSearch working** (now fixed) — eliminates the most common tool failure

### When semantic search becomes worth it

Semantic search earns its complexity when:
- The codebase is large (>500 files) and unfamiliar to the agent
- The agent needs to discover code it doesn't know exists
- Natural language queries can't be decomposed into grep-friendly keywords
- The same codebase is searched repeatedly (amortized index cost)

For Forge's current use case (self-improvement on its own ~20-file codebase), semantic search is overkill. The agent knows the files. `file_search("**/*.cs")` + `grep_search` covers everything.

## 5. Recommendation: Phased Approach

### Phase 1 (Now): Multi-Keyword Expansion — Option A

Implement `semantic_search` as a thin wrapper that:
1. Splits the query into keywords
2. Adds common code synonyms (auth→login, error→exception, test→assert)
3. Runs parallel `grep_search` calls
4. Deduplicates and ranks by match count

**Time:** 1 day. Ships immediately. Eliminates the "tool not found" error when the agent tries semantic search.

### Phase 2 (When needed): REPO.md + Structural Index — Options B + RIG

Build a pre-computed structural snapshot of the codebase:
- Parse `*.csproj`, `package.json`, etc. for build/dependency graph
- Parse code files with tree-sitter for definition-level index
- Generate a `REPO.md` with architecture overview
- Index definitions with BM25 for fast lookup

**Trigger:** When Forge works on unfamiliar codebases where exploration overhead >30% of total tokens.

### Phase 3 (If needed): Embedding Re-ranking — Option D

Add ONNX-based code embeddings to re-rank BM25 results:
- Use `jina-code-embeddings-0.5b` or `codet5p-110m-embedding`
- Only embed the top-50 BM25 candidates (not the whole codebase)
- Cache embeddings for the working set

**Trigger:** When BM25 recall proves insufficient (measurable via trajectory analysis showing "searched but didn't find relevant code").

## 6. References

| Source | Type | Key Insight |
|--------|------|-------------|
| Agentic RAG Survey (2025) | Paper | Hybrid BM25+embedding, iterative retrieval, query routing |
| RIG (2026) | Paper | Structural maps: +12.2% accuracy, -54% time |
| SWE-Adept (2026) | Paper | Tree-sitter + dependency DFS beats BFS |
| SWE-Pruner (2026) | Paper | 76% of tokens are reads; goal-aware pruning |
| HyFunc (2026) | Paper | Embedding retrieval in ~104ms via MLP cascade |
| MCP-Atlas (2026) | Paper | Tool discovery is the #1 failure mode (57%) |
| CMV (2026) | Paper | Structural trimming: 20-40% reduction, zero loss |
| Neural Paging (2026) | Paper | Context as semantic cache; O(N·K²) paging |
| Zoekt (Sourcegraph) | Tool | Trigram + BM25 + symbol-aware ranking |
| Bloop (archived) | Tool | Tantivy + Qdrant + tree-sitter + embeddings |
| CodeBERT/UniXcoder | Model | Code+NL pretrained models for code search |
| Jina Code Embeddings | Model | 200M-2B param code embedding models, ONNX-exportable |
