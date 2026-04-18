# Agentic Deep-Thinking RAG Pipeline

A production-quality **Retrieval-Augmented Generation** system built on the **Microsoft Agent Framework**, **Azure AI Foundry**, and **Tavily Search**. The repository ships two contrasting RAG pipelines — a **Deep-Thinking Agentic** pipeline and a **One-Shot** pipeline — so you can compare quality, latency, and architecture side by side.

---

## Two Pipelines, Same Infrastructure

| | **One-Shot RAG** | **Deep-Thinking Agentic RAG** |
|---|---|---|
| **Topology** | Linear (single pass) | Cyclical (agent-driven loop) |
| **Planning** | None — raw query hits search directly | Multi-step tool-aware plan |
| **Query optimization** | None | Per-step rewriting tuned to the search tool |
| **Retrieval scope** | Vector search only | Vector search + Web search (tool-selected) |
| **Post-retrieval** | Reranker → LLM answer | Reranker → Distiller → Reflection → Policy |
| **Memory** | Stateless | RAGState tracks plan, findings, and history |
| **Control flow** | One and done | Continue / Re-think / Finish loop |
| **Best for** | Simple factual questions | Complex multi-hop queries across sources |

See [`RAG.Comparison.md`](RAG.Comparison.md) for full ASCII architecture diagrams, a side-by-side query walkthrough, and LLM call count comparison.

---

## What Is Deep-Thinking RAG?

Standard RAG is a single-shot pattern: embed a query → retrieve chunks → stuff them into a prompt → generate. It works for simple lookups but breaks down for complex, multi-part questions that require connecting information across sources or combining internal documents with current web data.

**Deep-Thinking RAG** treats answering as a research process:

1. **Plan** — decompose the question into an ordered set of sub-questions, choosing the right tool for each step (`search_docs` for the internal knowledge base, `search_web` for live results).
2. **Rewrite** — rephrase each sub-question for optimal retrieval at that point in the research.
3. **Retrieve** — fetch from the vector store (semantic + keyword + hybrid) or the web via Tavily.
4. **Rerank** — precision-filter the top results using an LLM judge.
5. **Distil** — compress the reranked evidence into a dense, citation-preserving paragraph.
6. **Reflect** — evaluate whether the accumulated evidence is sufficient or more research is needed.
7. **Govern** — decide to continue (loop back) or finish (proceed to synthesis).
8. **Synthesise** — write a comprehensive, citable final answer from the complete research history.

This loop runs up to a configurable maximum of iterations, ensuring the model never stops too early or spins indefinitely.

---

## Architecture

### Deep-Thinking Agentic RAG

```
User Query
    │
[Gateway] ──── URL/file path detected → load & index source, done
    │           No source yet         → prompt for a URL, done
    │           Has source + query    ↓
[Planner]  ─── decomposes into N sub-questions
    │
[QueryRewriter] ◄──── StepSignal(n) ─── [Policy] ◄─────────────────┐
    │                                        │ FinishSignal          │
    ├── search_docs ──► [VectorSearch]       ▼                       │
    └── search_web  ──► [WebSearch]    [Synthesis] → final answer    │
                              │                                       │
                         [Reranker]                                   │
                              │                                       │
                         [Distiller]                                  │
                              │                                       │
                         [Reflection] ─── PolicySignal ──────────────┘
```

### One-Shot RAG

```
User Query
    │
[Gateway] ──── (same URL/source handling)
    │
[QueryBridge] ─── raw query, no rewriting, no planning
    │
[VectorSearch] ── broad recall (top-K)
    │
[Reranker] ────── precision filter (top-3)
    │
[OneShotAnswer] ─ single LLM call → final answer
```

See [`Agentic.RAG.Pipeline.md`](Agentic.RAG.Pipeline.md) for the full Mermaid block diagram.

---

## Projects

| Project | Description |
|---|---|
| `AgenticRAG` | Core library — executors, workflow graph, services, models |
| `AgenticRAG.Api` | ASP.NET Core host with DevUI chat window |

---

## Executor Nodes

### Deep-Thinking Pipeline

| Node | Role |
|---|---|
| **Gateway** | Root node. Routes incoming messages: loads sources, rejects premature queries, or passes through to the Planner |
| **Planner** | Decomposes the user question into a research plan with tool assignments |
| **QueryRewriter** | Rewrites each sub-question for precise retrieval |
| **VectorSearch** | Semantic + keyword + hybrid search against the in-memory vector store |
| **WebSearch** | Live web search via Tavily |
| **Reranker** | LLM-based precision filtering — keeps the top-K most relevant chunks |
| **Distiller** | Compresses evidence into a dense, citation-rich paragraph |
| **Reflection** | Evaluates research quality and updates the shared state |
| **Policy** | Decides: continue researching (→ QueryRewriter) or finish (→ Synthesis) |
| **Synthesis** | Writes the final, multi-hop answer with inline clickable citations |

### One-Shot Pipeline

| Node | Role |
|---|---|
| **Gateway** | Same root node (reused) — handles URL loading and source checks |
| **QueryBridge** | Passes the raw query directly to vector search — no LLM call, no rewriting |
| **VectorSearch** | Same retrieval (reused) — semantic + keyword + hybrid search |
| **Reranker** | Same precision filter (reused) — LLM-based top-K selection |
| **OneShotAnswer** | Single LLM call: raw reranked docs + query → answer. No distillation, no reflection |

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- **Azure AI Foundry** resource with deployments for:
  - A reasoning model (e.g. `gpt-4o`)
  - A fast model (e.g. `gpt-4o-mini`)
  - An embedding model (e.g. `text-embedding-3-large`)
- **Tavily** API key — [tavily.com](https://tavily.com)

---

## Configuration

Copy `appsettings.json` and create `appsettings.Local.json` (git-ignored) with your credentials:

```json
{
  "AzureAI": {
    "Endpoint": "https://YOUR_RESOURCE.cognitiveservices.azure.com/",
    "ApiKey": "YOUR_KEY",
    "ReasoningModel": "gpt-4o",
    "FastModel": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-large"
  },
  "Tavily": {
    "ApiKey": "YOUR_TAVILY_KEY"
  },
  "Pipeline": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "InitialRetrievalTopK": 10,
    "RerankerTopK": 3,
    "MaxIterations": 10
  }
}
```

---

## Running the DevUI

```bash
cd AgenticRAG.Api
dotnet run
```

Open **http://localhost:8888/devui** in your browser.

1. Select an agent from the dropdown:
   - **Agentic-RAG** — deep-thinking multi-hop pipeline with planning, reflection, and iterative retrieval
   - **OneShot-RAG** — single-pass linear pipeline for comparison
2. Paste a URL or file path to load a knowledge source — the pipeline will chunk and embed it. Documents are shared across both agents.
3. Ask any question. Try the same query in both agents to compare answer quality and depth.

Multiple sources can be added at any time and accumulate in the shared knowledge base.

---

## Running the Console Pipeline

```bash
cd AgenticRAG
dotnet run
```

At startup you'll be prompted to select a pipeline mode:

```
Select pipeline:
  [1] Deep-Thinking RAG  (multi-hop, agentic loop)
  [2] One-Shot RAG       (single-pass, linear)
```

Configure sources in `appsettings.Local.json` under `Knowledge.Sources`, or enter a query interactively at the prompt. Set `Knowledge.DefaultQuery` to skip the interactive prompt.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Agent orchestration | [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework) |
| LLM & embeddings | Azure AI Foundry (OpenAI models) |
| Web search | [Tavily](https://tavily.com) |
| Vector search | In-memory (cosine similarity + BM25-style hybrid) |
| Chat UI | Microsoft.Agents.AI.DevUI |
| Runtime | .NET 9, ASP.NET Core |
