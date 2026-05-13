# Foundry POC — Migration Plan

## Goal

Recreate the Agentic Deep-Thinking RAG pipeline using **Microsoft Foundry services**, replacing custom in-memory implementations with managed Azure services where applicable.

---

## Implementation Approach — Foundry GUI-First

### Philosophy

**Maximize Foundry portal / GUI-driven development.** Instead of writing C# workflow graphs and custom executors, the orchestration lives in the **Foundry Workflow visual builder**. Each pipeline stage becomes a **Foundry Prompt Agent** with native tools attached. Custom code is limited to what Foundry can't do natively (e.g., document ingestion/chunking).

### Architecture Shift

| Layer | Current (Code-First) | Foundry (GUI-First) |
|---|---|---|
| **Orchestration** | `AgenticRagWorkflow.cs` — C# graph wiring | Foundry Workflow visual builder (sequential + loop) |
| **Agents** | `Executor<T>` classes with typed messages | Foundry Prompt Agents defined in portal |
| **Retrieval** | In-memory `VectorStore` + custom `TavilyService` | Azure AI Search tool + Web Search tool (native) |
| **Reranking** | `RerankerExecutor` — LLM cross-encoder call | Azure AI Search Semantic Ranker (built-in) |
| **State** | `RagState` via `IWorkflowContext` | Foundry Workflow variables (JSON) |
| **Routing** | Conditional edges on message type | If/else nodes + for-each loops in visual builder |
| **Auth** | API keys in `appsettings.json` | Agent Identity + Entra ID RBAC |
| **Tools** | Custom service classes | Foundry Toolbox (single MCP endpoint) |
| **Document ingestion** | `DocumentLoader.cs` + in-memory store | **Azure AI Search Import Data wizard** — portal-driven chunking, embedding, indexing |

### What Lives in Foundry Portal (No Code)

| Component | Foundry Feature |
|---|---|
| Planner Agent | Prompt Agent with reasoning model + structured JSON output |
| Query Rewriter Agent | Prompt Agent with fast model |
| Vector Search | Azure AI Search tool (attached to agent or via Toolbox) |
| Web Search | Foundry Web Search tool (Bing-powered) |
| Reranker | Azure AI Search Semantic Ranker (automatic) |
| Distiller Agent | Prompt Agent with fast model |
| Reflection Agent | Prompt Agent with fast model |
| Policy Agent | Prompt Agent with reasoning model + structured JSON output |
| Synthesis Agent | Prompt Agent with reasoning model |
| One-Shot Answer Agent | Prompt Agent with reasoning model + AI Search tool |
| Pipeline Orchestration | Foundry Workflow (visual sequential + loop) |
| Tool Management | Foundry Toolbox |
| Authentication | Agent Identity + RBAC |
| Memory (optional) | Foundry Memory Store |

### What Still Requires Code

| Component | Why | Implementation |
|---|---|---|
| **None for POC** | Azure AI Search's Import Data wizard handles chunking, embedding, and indexing from the portal | N/A |

> **Note:** For production scenarios with dynamic URL ingestion (user pastes URL at runtime), a lightweight ingestion script may be needed. For the POC, pre-loading documents via the portal wizard is sufficient.

### Foundry Prompt Agents to Create

Each current executor maps to a Foundry Prompt Agent:

#### 1. Planner Agent
- **Model**: Reasoning (`gpt-4o`)
- **Instructions**: "Decompose the user query into 2–5 ordered sub-questions. Each step declares a tool: `search_docs` or `search_web`."
- **Output format**: JSON Schema (`{ "steps": [{ "subQuestion", "reasoning", "tool" }] }`)
- **Tools**: None (planning only)

#### 2. Query Rewriter Agent
- **Model**: Fast (`gpt-4o-mini`)
- **Instructions**: "Given a sub-question and prior research findings, rewrite it into an optimized search query for the target tool."
- **Output format**: Plain text (rewritten query)
- **Tools**: None (rewriting only)

#### 3. Search Agent (Retrieval + Reranking combined)
- **Model**: Fast (`gpt-4o-mini`)
- **Instructions**: "Search for relevant information to answer the sub-question. Always cite sources."
- **Tools**: 
  - Azure AI Search (with `vector_semantic_hybrid` query type — retrieval + reranking in one call)
  - Web Search (Bing-powered)
- **Note**: Strategy selection (vector/keyword/hybrid) handled by agent instructions or defaults to hybrid

#### 4. Distiller Agent
- **Model**: Fast (`gpt-4o-mini`)
- **Instructions**: "Compress the retrieved evidence into a single dense paragraph (≤300 words). Preserve exact facts, numbers, and citations."
- **Tools**: None

#### 5. Reflection Agent
- **Model**: Fast (`gpt-4o-mini`)
- **Instructions**: "Summarize the distilled context into a single factual sentence."
- **Output format**: Plain text (one-sentence summary)
- **Tools**: None

#### 6. Policy Agent
- **Model**: Reasoning (`gpt-4o`)
- **Instructions**: "Given the research plan, completed steps, and accumulated findings, decide: CONTINUE (more research needed) or FINISH (evidence is sufficient)."
- **Output format**: JSON Schema (`{ "action": "CONTINUE" | "FINISH" }`)
- **Tools**: None

#### 7. Synthesis Agent
- **Model**: Reasoning (`gpt-4o`)
- **Instructions**: "Using all accumulated research evidence, write a comprehensive, multi-hop answer with inline citations."
- **Tools**: None

#### 8. One-Shot Answer Agent (for One-Shot pipeline)
- **Model**: Reasoning (`gpt-4o`)
- **Instructions**: "Answer the question using the search results. Cite sources."
- **Tools**: Azure AI Search (with semantic reranking)

### Foundry Workflow — Deep-Thinking Pipeline

```
┌──────────────────────────────────────────────────────────────┐
│  Foundry Workflow: "Agentic-RAG"                             │
│                                                              │
│  [Ask Question] ─── $userQuery                               │
│       │                                                      │
│  [Invoke Agent: Planner]                                     │
│       │ save output as: $plan (JSON)                         │
│       │ set $stepIndex = 0                                   │
│       │ set $researchHistory = []                            │
│       │ set $distilledContexts = []                          │
│       │                                                      │
│  ┌─[For Each: $plan.steps]─────────────────────────────┐     │
│  │    │                                                │     │
│  │  [Invoke Agent: QueryRewriter]                      │     │
│  │    │ input: step, $researchHistory                   │     │
│  │    │ save output as: $rewrittenQuery                 │     │
│  │    │                                                │     │
│  │  [If/Else: step.tool == "search_docs"]              │     │
│  │    ├── Yes: [Invoke Agent: SearchAgent w/ AI Search] │     │
│  │    └── No:  [Invoke Agent: SearchAgent w/ Web Search]│     │
│  │    │ save output as: $searchResults                  │     │
│  │    │                                                │     │
│  │  [Invoke Agent: Distiller]                          │     │
│  │    │ input: $searchResults                           │     │
│  │    │ save output as: $distilledContext               │     │
│  │    │ append to $distilledContexts                    │     │
│  │    │                                                │     │
│  │  [Invoke Agent: Reflection]                         │     │
│  │    │ input: $distilledContext                        │     │
│  │    │ save output as: $reflection                     │     │
│  │    │ append to $researchHistory                      │     │
│  │    │                                                │     │
│  │  [Invoke Agent: Policy]                             │     │
│  │    │ input: $plan, $researchHistory, $stepIndex      │     │
│  │    │ save output as: $decision                       │     │
│  │    │                                                │     │
│  │  [If/Else: $decision.action == "FINISH"]            │     │
│  │    ├── Yes: [Go To: Synthesis]                       │     │
│  │    └── No:  $stepIndex++, [Continue loop]            │     │
│  │                                                     │     │
│  └─────────────────────────────────────────────────────┘     │
│                                                              │
│  [Invoke Agent: Synthesis]                                   │
│       │ input: $userQuery, $distilledContexts,               │
│       │        $researchHistory                              │
│       │                                                      │
│  [Send Message] ─── final answer to user                     │
└──────────────────────────────────────────────────────────────┘
```

### Foundry Workflow — One-Shot Pipeline

```
┌──────────────────────────────────────────────────────────┐
│  Foundry Workflow: "OneShot-RAG"                         │
│                                                          │
│  [Ask Question] ─── $userQuery                           │
│       │                                                  │
│  [Invoke Agent: OneShotAnswer w/ AI Search tool]         │
│       │ AI Search returns results with semantic reranking │
│       │ Agent generates answer with citations             │
│       │                                                  │
│  [Send Message] ─── final answer to user                 │
└──────────────────────────────────────────────────────────┘
```

### Implementation Order

| Phase | What | Where |
|---|---|---|
| **Phase 1** | Azure AI Search index + document ingestion | Azure Portal: Import Data wizard (Feature 1) |
| **Phase 2** | Create Foundry Prompt Agents | Foundry Portal: define 8 agents with instructions, models, tools |
| **Phase 3** | Create Foundry Toolbox | Foundry Portal: bundle AI Search + Web Search tools (Feature 2) |
| **Phase 4** | Build One-Shot Workflow | Foundry Portal: simple sequential workflow (validate tools work) |
| **Phase 5** | Build Deep-Thinking Workflow | Foundry Portal: sequential + for-each + if/else + go-to (Feature 4) |
| **Phase 6** | Agent Identity & RBAC | Azure Portal: replace API keys with managed identity (Feature 5) |
| **Phase 7** | Memory (optional) | Foundry Portal: add memory store for cross-session continuity (Feature 3) |

---

## Feature 1 — Azure AI Search as Vector Database

### Current State

The pipeline uses an **in-memory `VectorStore`** (`Services/VectorStore.cs`) that:
- Stores `RagDocument` objects in a `List<RagDocument>`
- Implements `VectorSearch` (cosine similarity), `KeywordSearch` (term frequency), and `HybridSearch` (combined scoring)
- Is populated by `DocumentLoader`, which chunks and embeds documents using Azure AI embeddings
- Is queried by `VectorSearchExecutor`, which uses a fast LLM "Retrieval Supervisor" to pick the search strategy (`vector` / `keyword` / `hybrid`)

**Limitations of current approach:**
- No persistence — index is rebuilt on every startup
- No scalability — everything lives in process memory
- BM25-style keyword search is a simplified approximation
- No managed filtering, faceting, or scoring profiles

### Target State

Replace the in-memory `VectorStore` with **Azure AI Search**, connected via the Foundry `AIProjectClient`. This gives us:
- Persistent, managed vector index
- Native `vector`, `keyword`, `semantic`, `vector_simple_hybrid`, and `vector_semantic_hybrid` query types (maps directly to the existing strategy supervisor)
- Built-in BM25 for keyword search
- **Built-in Semantic Ranker** — replaces the LLM-based `RerankerExecutor` entirely (see below)
- OData filters on metadata fields (e.g., `Section`, `Source`)

### Built-in Reranking — Eliminating the LLM Reranker

**Current approach:** `RerankerExecutor` uses a fast LLM call as a cross-encoder proxy — it sends all candidate documents + the query to `gpt-4o-mini`, asks it to return ranked indices, then keeps the top-K.

**Azure AI Search approach:** The [Semantic Ranker](https://learn.microsoft.com/en-us/azure/search/semantic-search-overview) is a native L2 reranker built into Azure AI Search:
- Uses Microsoft's deep learning models (from Bing) to rerank results by semantic relevance
- Runs automatically when `query_type` includes `semantic` (e.g., `vector_semantic_hybrid`)
- Assigns a `@search.rerankerScore` (0.0–4.0) to each result
- Processes up to top-50 initial results → reranks → returns in relevance order
- Also produces **semantic captions** (extractive summaries) that could feed the Distiller

**What this means for the pipeline:**
| Current Step | Foundry Replacement |
|---|---|
| `VectorSearchExecutor` → broad recall (top-10) | Azure AI Search retrieval (top-K) |
| `RerankerExecutor` → LLM cross-encoder (top-3) | Azure AI Search semantic ranker (built-in, top-K with `@search.rerankerScore`) |
| 2 separate stages, 1 extra LLM call | **1 combined query** — retrieval + reranking in a single Azure AI Search call |

**Impact:** Eliminates one LLM call per retrieval step (saves latency + tokens). The `VectorSearchExecutor` can request `top_k=10` with `vector_semantic_hybrid` and the results come back pre-reranked. We take the top-3 by `@search.rerankerScore` and send them directly to the Distiller.

**Semantic captions bonus:** Azure AI Search can return extractive captions (~200 words) per result, which could optionally augment or simplify the Distiller's job.

### Reference

- [Foundry Agents — Azure AI Search Tool](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/ai-search?tabs=keys%2Cportal&pivots=csharp)

### Azure AI Search Index Schema

Design the index to mirror `RagDocument` fields:

| Index Field | Type | Searchable | Retrievable | Filterable | Notes |
|---|---|---|---|---|---|
| `id` | `Edm.String` | — | ✅ | ✅ | Unique chunk ID (key) |
| `content` | `Edm.String` | ✅ | ✅ | — | Chunk text |
| `source` | `Edm.String` | — | ✅ | ✅ | Document name (e.g., "NVIDIA 2024 10-K") |
| `section` | `Edm.String` | ✅ | ✅ | ✅ | Section metadata for scoped retrieval |
| `url` | `Edm.String` | — | ✅ | — | Source URL for citations |
| `embedding` | `Collection(Edm.Single)` | ✅ (vector) | — | — | Vector field for semantic search |

### Document Ingestion Flow (Portal-Driven)

Azure AI Search's **Import Data wizard** handles the entire ingestion pipeline from the Azure portal — no custom code needed:

```
Upload documents to Azure Blob Storage
    │
[Azure Portal → AI Search → Import Data]
    │
    ├── Connect to Data Source (Blob Storage)
    ├── Vectorize Text (Azure OpenAI embedding model)
    │     Built-in chunking: pages, 2000 chars, 500 overlap
    ├── Enable Semantic Ranking (automatic reranking)
    └── Create Index + Indexer + Skillset
```

**Reference:** [Import and vectorize data — Azure AI Search](https://learn.microsoft.com/en-us/azure/search/search-get-started-portal-import-vectors)

#### Portal Steps

1. **Upload source documents** to Azure Blob Storage (PDFs, HTML saved as files, text files)
2. **Azure AI Search → Import Data** wizard:
   - **Data source**: Azure Blob Storage container
   - **Vectorize text**: Select Azure OpenAI or Foundry embedding model (e.g., `text-embedding-3-small`)
   - **Authentication**: System-assigned managed identity
   - **Semantic ranking**: Enable on the Advanced settings page
3. **Auto-generated index fields**:
   - `chunk_id` — unique chunk identifier
   - `chunk` — text content (searchable, retrievable)
   - `title` — document title (retrievable, filterable)
   - `text_vector` — embedding vector (searchable)
   - `parent_id` — source document reference
4. **Indexer runs automatically** — re-indexes on schedule or on-demand

#### For Dynamic URL Ingestion (Future)

The portal wizard works for pre-loaded documents. For runtime URL ingestion (user pastes URL in chat), a lightweight script may be needed later. For the POC, pre-loading via portal is sufficient.

### Setup Steps (All Portal / GUI)

#### 1. Create Azure AI Search Service
- **Portal**: Create an Azure AI Search resource (Basic tier or higher for managed identity)
- Enable **role-based access control**
- Enable **system-assigned managed identity**

#### 2. Deploy Embedding Model
- **Foundry Portal**: Deploy `text-embedding-3-small` (or `text-embedding-3-large`) in your Foundry project

#### 3. Upload Documents to Blob Storage
- Create a storage container
- Upload source documents (e.g., NVIDIA 10-K PDF/HTML)
- Assign **Storage Blob Data Reader** role to AI Search managed identity

#### 4. Run Import Data Wizard
- **Azure AI Search Portal → Import Data**
- Select **RAG** scenario
- Connect to Blob Storage container
- Select embedding model (from Foundry or Azure OpenAI)
- Enable **semantic ranking** on Advanced settings
- Wizard auto-creates: index, indexer, skillset

#### 5. Verify Index
- **Azure AI Search Portal → Indexes** → confirm documents indexed
- **Search Explorer** → test queries to validate results

### What This Replaces (from the code-first pipeline)

- `VectorStore.cs` — replaced by Azure AI Search managed index
- `DocumentLoader.cs` — replaced by Import Data wizard (portal)
- `RerankerExecutor.cs` — replaced by Azure AI Search Semantic Ranker
- `VectorSearchExecutor.cs` — replaced by Azure AI Search tool on Foundry agents
- In-memory embeddings — replaced by integrated vectorization in the wizard
- Manual chunking logic — replaced by built-in chunking (2000 chars, 500 overlap)

---

## Future Features (Planned)

- **Feature 6** — Foundry Evaluation for pipeline quality metrics

---

## Feature 5 — Foundry Agent Identity (Security & Authentication)

### Reference

- [Agent Identity in Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/agent-identity)

### What Is Agent Identity?

Foundry Agent Identity is a **Microsoft Entra ID-based identity framework** purpose-built for AI agents. It eliminates embedded secrets by giving each agent its own service principal with OAuth 2.0 token exchange at runtime.

### Key Concepts

| Concept | Description |
|---|---|
| **Agent Identity** | A special service principal in Entra ID representing the agent at runtime |
| **Agent Identity Blueprint** | A reusable governing template (like a class) for a category of agents |
| **Shared Project Identity** | All unpublished/dev agents in a project share one identity — simpler admin |
| **Distinct Agent Identity** | Published agents get their own identity — independent permissions & audit |
| **Federated Credential** | Blueprint trusts the project's managed identity — no stored secrets |

### Authentication Flows

| Flow | When Used | How It Works |
|---|---|---|
| **Unattended (app-only)** | Agent acts on its own authority | Client credentials flow → agent identity token → scoped resource token |
| **Attended (on-behalf-of)** | Agent acts on behalf of a user | User authenticates → OBO flow → token carries both agent + user permissions |

### Runtime Token Exchange (Automatic)

```
Agent invokes tool
    │
[1] Blueprint Authentication
    │   Agent Service presents blueprint OAuth credentials to Entra ID
    │
[2] Agent Identity Token Issued
    │   Entra ID validates → issues agent identity token
    │
[3] Scoped Token Request
    │   Agent Service requests access token for downstream service audience
    │   (e.g., https://storage.azure.com, https://search.azure.com)
    │
[4] Authenticated Tool Call
    │   Scoped token passed to MCP server / A2A endpoint / Azure service
    └── Resource validates token + checks RBAC → grants/denies access
```

Developers never manage tokens directly — Agent Service handles the entire exchange.

### How It Fits This Pipeline

Currently the pipeline uses **API keys** for authentication:

| Current | With Agent Identity |
|---|---|
| `AzureAI.ApiKey` in `appsettings.json` | Managed identity → no API key needed |
| `Tavily.ApiKey` in `appsettings.json` | Replaced by Foundry Web Search (Feature 2) — no key |
| Azure AI Search key-based auth | Agent identity with `Search Index Data Contributor` role |
| Secrets in config files (git-ignored) | Zero secrets — Entra ID + RBAC only |

### RBAC Role Assignments for This Pipeline

| Resource | Required Role | Why |
|---|---|---|
| Azure AI Search | `Search Index Data Contributor` | Read/write index for document ingestion and querying |
| Azure AI Search | `Search Service Contributor` | Manage index schema |
| Azure AI Foundry | `Azure AI User` | Call LLM models and embeddings |
| Foundry Project | `Azure AI User` | Access project resources, toolbox, memory |

```bash
# Assign roles to agent identity
az role assignment create \
    --assignee "<agentIdentityId>" \
    --role "Search Index Data Contributor" \
    --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Search/searchServices/<search>"

az role assignment create \
    --assignee "<agentIdentityId>" \
    --role "Azure AI User" \
    --scope "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<foundry>"
```

### Changes Required

#### 1. Replace key-based auth with `DefaultAzureCredential`

```csharp
// Before (API key)
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new ApiKeyCredential(apiKey));

// After (managed identity)
var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential());
```

#### 2. Update `AppSettings.cs`

Remove `ApiKey` fields from `AzureAISettings` and `TavilySettings`. Add `ProjectEndpoint` to `FoundrySettings`.

#### 3. Update `AzureAIService.cs`

Replace `ApiKeyCredential` with `DefaultAzureCredential` for all Azure OpenAI and embedding calls.

#### 4. Update `AzureSearchStore.cs` (Feature 1)

Use `DefaultAzureCredential` for Azure AI Search client instead of admin keys.

#### 5. Configuration Changes

```json
{
  "Foundry": {
    "ProjectEndpoint": "https://<resource>.ai.azure.com/api/projects/<project>"
  }
}
```

Remove:
```json
{
  "AzureAI": {
    "ApiKey": "..."
  },
  "Tavily": {
    "ApiKey": "..."
  }
}
```

### Security Best Practices

- **Least privilege**: Assign only the roles the agent needs, at the narrowest scope (resource-level, not subscription)
- **Publish for production**: Published agents get distinct identities with independent audit trails
- **No secrets in code/config**: All auth via Entra ID federated credentials
- **Review external tool access**: If tools call non-Microsoft services, data handling depends on the external provider

---

## Feature 4 — Foundry Workflow UI (Visual Pipeline Orchestration)

### Reference

- [Workflows in Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/workflow)
- [Declarative (Low-code) workflows in VS Code](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/vs-code-agents-workflow-low-code)
- [Hosted (Pro-code) workflows in VS Code](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/vs-code-agents-workflow-pro-code)

### What Is Foundry Workflow?

Foundry Workflows are **UI-based, declarative orchestration** tools for building agent pipelines visually. Key capabilities:

| Capability | Description |
|---|---|
| **Visual builder** | Drag-and-drop node sequencing in the Foundry portal |
| **Agent nodes** | Invoke any Foundry agent (prompt agents with tools) |
| **Logic nodes** | If/else branching, go-to, for-each loops |
| **Data transformation** | Set variables, parse values, structured JSON output |
| **Human-in-the-loop** | Ask questions, approval gates |
| **Power Fx expressions** | Excel-like formulas for conditions and data manipulation |
| **YAML view** | Toggle between visual and YAML editing; version history |
| **Orchestration patterns** | Sequential, Group Chat (dynamic handoff), Human-in-the-loop |

### How It Maps to the Current Pipeline

The current pipeline uses **code-defined workflow graphs** (`AgenticRagWorkflow.cs` / `OneShotRagWorkflow.cs`) with typed message routing via the Microsoft Agent Framework. Foundry Workflow could provide a **visual alternative**:

| Current (Code) | Foundry Workflow (Visual) |
|---|---|
| `AgenticRagWorkflow.cs` — C# graph wiring | Visual node sequencing in Foundry portal |
| `Executor<T>` classes with typed messages | Agent nodes invoking Foundry prompt agents |
| Conditional edges (`SearchRequest.Tool == "search_docs"`) | If/else logic nodes with Power Fx conditions |
| `RagState` shared via `IWorkflowContext` | Variables set/passed between nodes |
| Policy loop (`CONTINUE` → `QueryRewriter`) | Go-to node looping back to earlier steps |
| `FinishSignal` → `Synthesis` | Sequential flow to final agent node |

### Potential Workflow Design

```
┌──────────────────────────────────────────────────────────┐
│  Foundry Workflow: "Deep-Thinking RAG"                   │
│                                                          │
│  [Ask Question] ─── user input                           │
│       │                                                  │
│  [Invoke Agent: Planner] ─── decompose into steps        │
│       │ save output as: $plan (JSON)                     │
│       │                                                  │
│  [For Each: $plan.steps] ──────────────────────────┐     │
│       │                                            │     │
│  [If/Else: step.tool == "search_docs"]             │     │
│       ├── Yes: [Invoke Agent: VectorSearch+Rerank]  │     │
│       └── No:  [Invoke Agent: WebSearch]            │     │
│       │                                            │     │
│  [Invoke Agent: Distiller]                         │     │
│       │                                            │     │
│  [Invoke Agent: Reflection]                        │     │
│       │ save output as: $research_history           │     │
│       │                                            │     │
│  [Invoke Agent: Policy]                            │     │
│       │ save output as: $decision                   │     │
│       │                                            │     │
│  [If/Else: $decision == "FINISH"]                  │     │
│       ├── Yes: [Go To: Synthesis]                   │     │
│       └── No:  [Continue loop]                      │     │
│  └─────────────────────────────────────────────────┘     │
│                                                          │
│  [Invoke Agent: Synthesis] ─── final answer              │
│       │                                                  │
│  [Send Message] ─── return to user                       │
└──────────────────────────────────────────────────────────┘
```

### Two Approaches: Low-Code vs Pro-Code

| Approach | Tool | Best For |
|---|---|---|
| **Declarative (Low-code)** | Foundry portal visual builder or VS Code YAML | Rapid prototyping, non-developers, visual debugging |
| **Hosted (Pro-code)** | VS Code with Microsoft Agent Framework code | Full control, custom logic, complex state management |

The current codebase is pro-code. Foundry Workflow could serve as:
1. **A visual prototype** to validate pipeline flow before coding
2. **A simplified version** for non-developer stakeholders to experiment with
3. **A full replacement** if the visual builder supports the required loop/state complexity

### Important Limitations

- **Hosted agents are NOT supported** in the workflow designer — only prompt agents
- Loop complexity (Policy → QueryRewriter cycle) may need careful mapping to for-each + go-to nodes
- Shared mutable state (`RagState`) is harder to model with simple variables
- The visual builder is best for **sequential and branching** patterns; the deep-thinking pipeline's **cyclic** pattern pushes its limits

### Recommendation

Use Foundry Workflow for:
- ✅ **One-Shot pipeline** — linear, maps perfectly to sequential workflow
- ✅ **Prototyping** the deep-thinking flow visually
- ⚠️ **Deep-thinking pipeline** — possible but may require workarounds for the research loop

Keep the pro-code approach (`AgenticRagWorkflow.cs`) for production deep-thinking pipeline where fine-grained control over state and routing is critical.

---

## Feature 3 — Foundry Memory (Optional / Future)

### Reference

- [Memory in Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/memory-usage?pivots=csharp)

### What Is Foundry Memory?

Foundry Memory is a **managed, long-term memory** service that enables agent continuity across sessions, devices, and workflows. It provides:
- **Persistent user profiles** — auto-extracted preferences and facts (e.g., "user prefers dark roast coffee")
- **Chat summaries** — compressed conversation history retained across sessions
- **Scoped isolation** — `scope` parameter segments memory per user/team for secure, isolated experiences
- **Two memory types:**
  - **Static memories** (user profile) — injected at the start of each conversation
  - **Contextual memories** (chat summary) — retrieved per turn based on semantic relevance

### How It Could Fit This Pipeline

Currently `RagState` is the pipeline's shared memory, but it's **ephemeral** — it exists only for one query lifecycle. Foundry Memory could add a persistent layer:

| Current State | With Foundry Memory |
|---|---|
| `RagState` is per-query, in-memory | `RagState` stays per-query (unchanged) |
| No cross-session memory | Agent remembers user preferences across sessions |
| No conversation history | Chat summaries retained and searchable |
| User must re-provide context each time | Agent recalls prior questions and research patterns |

### Potential Use Cases

1. **User preference persistence** — remember preferred document sources, topics of interest, research depth preferences
2. **Cross-session research continuity** — "last time you asked about NVIDIA's risk factors, here's what we found"
3. **Personalized synthesis** — tailor answer style/depth based on remembered user profile
4. **Multi-turn context** — within the DevUI, carry context across separate query sessions

### How It Works (C# SDK)

```csharp
// Create a memory store
MemoryStoreDefaultDefinition definition = new(
    chatModel: "gpt-4o",
    embeddingModel: "text-embedding-3-small"
);
definition.Options = new(
    isUserProfileEnabled: true,
    isChatSummaryEnabled: true);

MemoryStore memoryStore = projectClient.MemoryStores.CreateMemoryStore(
    name: "rag-memory",
    definition: definition,
    description: "RAG pipeline user memory"
);

// Attach to agent via MemorySearchPreviewTool
agentDefinition.Tools.Add(new MemorySearchPreviewTool(
    memoryStoreName: "rag-memory",
    scope: "{{$userId}}")  // auto-resolved per user
{
    UpdateDelayInSecs = 300  // update after 5 min inactivity
});
```

### Integration Points (When Ready)

| Component | Change |
|---|---|
| `RagWorkflowChatClient.cs` | Attach `MemorySearchPreviewTool` to agent definition |
| `SynthesisExecutor.cs` | Optionally query memory for prior research on same topic |
| `GatewayExecutor.cs` | Store loaded source preferences in user memory |
| `AppSettings.cs` | Add `MemoryStoreName` to Foundry config |

### Why "Optional / Future"

- The current pipeline is **query-scoped** — each question is self-contained
- Memory adds most value in **multi-session, multi-user** scenarios (e.g., a persistent assistant)
- Requires careful scoping design (per-user vs. per-team vs. global)
- Preview feature — API may evolve

---

## Feature 2 — Foundry Toolbox (Centralized Tool Management)

### Reference

- [Foundry Toolbox](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox?pivots=dotnet)

### What Is Toolbox?

Foundry Toolbox is a **managed tool registry** that bundles multiple tools (Azure AI Search, Web Search, MCP servers, Code Interpreter, etc.) into a single resource with:
- **One MCP-compatible endpoint** — any agent framework can consume it
- **Centralized credential management** — no more scattered API keys
- **Versioning** — create, test, and promote tool configurations without code changes
- **Governance** — token refresh, policy enforcement, and audit at runtime

### How It Fits This Pipeline

Currently the pipeline manages tools independently:

| Current Tool | Current Integration | Toolbox Replacement |
|---|---|---|
| Azure AI Search | Direct `SearchClient` calls (Feature 1) | `azure_ai_search` tool in Toolbox |
| Web Search (Tavily) | Custom `TavilyService` + separate API key | `web_search` tool in Toolbox (Bing-powered, no Tavily key needed) |

With Toolbox, both tools are accessed through a **single MCP endpoint**:

```
{project_endpoint}/toolboxes/rag-tools/mcp?api-version=v1
```

### Toolbox Definition

```csharp
// Create the toolbox with both search tools
AgentToolboxes toolboxClient = projectClient.AgentAdministrationClient.GetAgentToolboxes();

ProjectsAgentTool aiSearchTool = ProjectsAgentTool.AsProjectTool(
    ResponseTool.CreateAzureAISearchTool(new AzureAISearchToolOptions(indexes: [
        new AzureAISearchToolIndex {
            ProjectConnectionId = aiSearchConnection.Id,
            IndexName = "rag-chunks",
            TopK = 10,
            QueryType = AzureAISearchQueryType.VectorSemanticHybrid
        }
    ])));

ProjectsAgentTool webSearchTool = ProjectsAgentTool.AsProjectTool(
    ResponseTool.CreateWebSearchTool());

ToolboxVersion toolbox = await toolboxClient.CreateToolboxVersionAsync(
    toolboxName: "rag-tools",
    tools: [aiSearchTool, webSearchTool],
    description: "RAG pipeline tools — AI Search + Web Search"
);
```

### What This Replaces

| Removed Component | Replaced By |
|---|---|
| `TavilyService.cs` | Foundry Toolbox `web_search` (Bing-powered) |
| `TavilySettings` in `AppSettings.cs` | Toolbox manages credentials |
| Separate Tavily API key | No external key needed — Foundry handles auth |
| Direct `SearchClient` in `AzureSearchStore.cs` | Toolbox `azure_ai_search` (optional — can still use direct client for indexing) |

### Important Nuance: Indexing vs. Querying

Toolbox handles **querying** (search tools invoked by agents), but **document indexing** (pushing chunks to Azure AI Search) still requires the direct `SearchClient` SDK. So:

- **Indexing** (Feature 1): `DocumentLoader` → `SearchClient.IndexDocumentsAsync()` — unchanged
- **Querying**: `VectorSearchExecutor` and `WebSearchExecutor` → invoke tools via Toolbox MCP endpoint

### Changes Required

#### 1. Additional NuGet Packages
```
Azure.AI.Projects (already in Feature 1)
```

#### 2. Configuration Updates

Replace Tavily config with Toolbox config:
```json
{
  "Foundry": {
    "ProjectEndpoint": "https://<resource>.ai.azure.com/api/projects/<project>",
    "SearchConnectionName": "my-search-connection",
    "SearchIndexName": "rag-chunks",
    "ToolboxName": "rag-tools"
  }
}
```

Remove:
```json
{
  "Tavily": {
    "ApiKey": "..."
  }
}
```

#### 3. Replace `TavilyService.cs` → Foundry Web Search

The Foundry [Web Search tool](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/web-overview) is the recommended replacement for Tavily:

| | **Tavily (Current)** | **Foundry Web Search** |
|---|---|---|
| **Search engine** | Tavily proprietary | Bing (managed by Microsoft) |
| **Extra resources** | Separate Tavily API key | None — included with Foundry project |
| **Geo-relevance** | No | `user_location` parameter |
| **Domain restriction** | No | Bing Custom Search integration (allow/block lists) |
| **Search context size** | Fixed | `search_context_size`: low / medium / high |
| **Result format** | Custom JSON → mapped to `RagDocument` | Agent-native with inline citations + URLs |
| **Status** | 3rd party | GA on Foundry |

**How it works in the pipeline:**
1. Agent identifies information gaps → constructs search queries
2. Web Search tool submits queries to Bing → retrieves results
3. Results include source URLs for citation attribution
4. `WebSearchExecutor` maps results to `RagDocument` records for downstream compatibility

Can be used **standalone** (direct tool call) or **via Toolbox** (bundled with Azure AI Search in a single MCP endpoint).

#### 4. Update `WebSearchExecutor.cs`

- Call the Toolbox MCP endpoint for web search
- Results still mapped to `RagDocument` records for downstream compatibility

#### 5. Optional: `VectorSearchExecutor.cs` via Toolbox

Two approaches:
- **Option A** (Recommended): Keep direct `SearchClient` for querying (more control over query parameters, scoring profiles, filters)
- **Option B**: Route queries through the Toolbox `azure_ai_search` tool (simpler, fully managed, but less query customization)

#### 6. Retire `TavilyService.cs` and `TavilySettings`

No longer needed — Foundry Toolbox provides web search natively.

### What Stays the Same

- `DocumentLoader.cs` — still uses `SearchClient` to push documents to Azure AI Search
- All downstream executors (Distiller, Reflection, Policy, Synthesis) — unchanged
- Message contracts — `SearchResults` still used by `WebSearchExecutor`
- Workflow graph — unchanged

---
