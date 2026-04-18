# Agentic Deep-Thinking RAG Pipeline — Mermaid Block Diagram

> This file contains the **Mermaid renderable block diagram** for the deep-thinking pipeline.
> For architecture descriptions, executor details, and One-Shot vs Deep-Thinking comparison, see [`README.md`](README.md) and [`RAG.Comparison.md`](RAG.Comparison.md).

## Block Diagram

```mermaid
flowchart LR

  %% ── Input Layer ──────────────────────────────────────────────
  subgraph INPUT["📥 Input"]
    DATA["Unstructured &\nStructured Data"]
    QUERY["Multi-Source\nMulti-Hop Query"]
  end

  %% ── Pre-processing ───────────────────────────────────────────
  subgraph PREPROC["🔧 Pre-processing"]
    CLEAN["Clean & Reduce\nTokens"]
  end

  %% ── Strategic Planning ───────────────────────────────────────
  subgraph PLANNING["🧠 Strategic Planning / Query Formulation"]
    PLANNER["Tool-Aware\nPlanner"]
    REWRITER["Query\nRewriter Agent"]
    META["Metadata-Aware\nChunking"]
    PLANNER --> REWRITER
    PLANNER --> META
  end

  %% ── Vector Strategy ──────────────────────────────────────────
  subgraph VECTOR["🔍 Vector Strategy Choosing Agent"]
    HYBRID["Hybrid\nSearch"]
    KEYWORD["Keyword\nSearch"]
    SEMANTIC["Semantic\nSearch"]
  end

  %% ── Retrieval Funnel ─────────────────────────────────────────
  subgraph RETRIEVAL["📚 Retrieval Funnel"]
    SUPER["Supervisor\nAgent"]
    CROSS["Cross\nEncoder"]
    DISTILL["Contextual\nDistillation"]
    WEBSEARCH["Web\nSearch"]
    SUPER --> CROSS
    SUPER --> WEBSEARCH
    CROSS --> DISTILL
  end

  %% ── Reflection ───────────────────────────────────────────────
  subgraph REFLECT["🪞 Update & Reflect"]
    CUMULATIVE["Cumulative\nResearch Memory"]
  end

  %% ── Self-Critique & Control Flow ─────────────────────────────
  subgraph CRITIQUE["⚙️ Self-Critique & Control Flow Policy"]
    POLICY["Policy\nAgent"]
    POLICY -->|"Continue"| PLANNING
    POLICY -->|"Re-thinking"| PLANNING
  end

  %% ── Output ───────────────────────────────────────────────────
  subgraph OUTPUT["💡 Output"]
    RESPONSE["Final\nResponse"]
  end

  %% ── Evaluation ───────────────────────────────────────────────
  subgraph EVAL["📊 Evaluation of Reasoning Engine"]
    QUALITATIVE["Qualitative\n(LLM Judge:\nFaithfulness, Relevance,\nSoundness, Depth)"]
    QUANTITATIVE["Quantitative\n(Precision &\nRecall)"]
    PERFORMANCE["Performance\n(Latency &\nToken Cost)"]
  end

  %% ── Main Flow ────────────────────────────────────────────────
  DATA --> PREPROC
  QUERY --> PLANNING
  PREPROC --> PLANNING

  PLANNING --> VECTOR
  VECTOR --> RETRIEVAL

  RETRIEVAL --> REFLECT
  REFLECT --> CRITIQUE
  CRITIQUE -->|"Finish"| OUTPUT
  OUTPUT --> EVAL
```

---

