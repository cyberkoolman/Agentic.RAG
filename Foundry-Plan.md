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
│  ┌─[For Each: $plan.steps]───────────────────────────────┐   │
│  │    │                                                  │   │
│  │  [Invoke Agent: QueryRewriter]                        │   │
│  │    │ input: step, $researchHistory                    │   │
│  │    │ save output as: $rewrittenQuery                  │   │
│  │    │                                                  │   │
│  │  [If/Else: step.tool == "search_docs"]                │   │
│  │    ├── Yes: [Invoke Agent: SearchAgent w/ AI Search]  │   │
│  │    └── No:  [Invoke Agent: SearchAgent w/ Web Search] │   │
│  │    │ save output as: $searchResults                   │   │
│  │    │                                                  │   │
│  │  [Invoke Agent: Distiller]                            │   │
│  │    │ input: $searchResults                            │   │
│  │    │ save output as: $distilledContext                │   │
│  │    │ append to $distilledContexts                     │   │
│  │    │                                                  │   │
│  │  [Invoke Agent: Reflection]                           │   │
│  │    │ input: $distilledContext                         │   │
│  │    │ save output as: $reflection                      │   │
│  │    │ append to $researchHistory                       │   │
│  │    │                                                  │   │
│  │  [Invoke Agent: Policy]                               │   │
│  │    │ input: $plan, $researchHistory, $stepIndex       │   │
│  │    │ save output as: $decision                        │   │
│  │    │                                                  │   │
│  │  [If/Else: $decision.action == "FINISH"]              │   │
│  │    ├── Yes: [Go To: Synthesis]                        │   │
│  │    └── No:  $stepIndex++, [Continue loop]             │   │
│  │                                                       │   │
│  └───────────────────────────────────────────────────────┘   │
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
┌──────────────────────────────────────────────────────────────┐
│  Foundry Workflow: "OneShot-RAG"                             │
│                                                              │
│  [Ask Question] ─── $userQuery                               │
│       │                                                      │
│  [Invoke Agent: OneShotAnswer w/ AI Search tool]             │
│       │ AI Search returns results with semantic reranking    │
│       │ Agent generates answer with citations                │
│       │                                                      │
│  [Send Message] ─── final answer to user                     │
└──────────────────────────────────────────────────────────────┘
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

## Step-by-Step Instructions

### Prerequisites

Before starting, ensure you have:

- [ ] An **Azure subscription** with Contributor access
- [ ] A **Microsoft Foundry project** created at [ai.azure.com](https://ai.azure.com)
- [ ] The following model deployments in your Foundry project:
  - `gpt-4o` (reasoning model)
  - `gpt-4o-mini` (fast model)
  - `text-embedding-3-small` (embedding model)
- [ ] A sample document for testing (e.g., [NVIDIA 2024 10-K](https://www.sec.gov/cgi-bin/browse-edgar?action=getcompany&CIK=0001045810&type=10-K))

---

### Phase 1 — Azure AI Search Index + Document Ingestion

**Goal:** Get your source documents chunked, embedded, and indexed in Azure AI Search — all from the portal.

#### Step 1.1 — Create Azure AI Search Service

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **+ Create a resource** → search for **"Azure AI Search"**
3. Click **Create** and fill in:
   - **Subscription**: your subscription
   - **Resource group**: create new or use existing (e.g., `rg-foundry-rag`)
   - **Service name**: e.g., `search-foundry-rag`
   - **Location**: same region as your Foundry project
   - **Pricing tier**: **Basic** (minimum for managed identity + semantic ranker)
4. Click **Review + Create** → **Create**
5. After deployment, go to the resource

#### Step 1.2 — Enable RBAC and Managed Identity

1. In your AI Search resource, go to **Settings → Keys**
2. Set **API Access Control** to **"Role-based access control"** (or "Both" for transition)
3. Go to **Settings → Identity**
4. Under **System assigned**, toggle **Status** to **On** → click **Save**
5. Go to **Access control (IAM)** → **Add role assignment**:
   - Assign yourself: **Search Service Contributor** + **Search Index Data Contributor**

#### Step 1.3 — Upload Documents to Blob Storage

1. Go to [Azure Portal](https://portal.azure.com) → **+ Create a resource** → **Storage account**
   - **Name**: e.g., `stfoundryrag`
   - **Region**: same as AI Search
   - **Performance**: Standard
   - **Redundancy**: LRS (fine for POC)
2. Click **Create**
3. Go to the storage account → **Data storage → Containers** → **+ Container**
   - **Name**: `rag-documents`
4. Click into the container → **Upload**
   - Upload your source documents (PDF, HTML files, text files)
   - Example: save the NVIDIA 10-K as a PDF and upload it
5. Go to the storage account → **Access control (IAM)** → **Add role assignment**:
   - **Role**: `Storage Blob Data Reader`
   - **Assign to**: your AI Search service's managed identity

#### Step 1.4 — Run the Import Data Wizard

1. Go to your **Azure AI Search** resource in the portal
2. On the **Overview** page, click **Import data**
3. Select **Azure Blob Storage** as the data source
4. Click **RAG** as the scenario

**Connect to your data:**
5. Select your subscription → storage account → `rag-documents` container
6. Check **"Authenticate using managed identity"** → System-assigned
7. Click **Next**

**Vectorize your text:**
8. Select **Azure OpenAI** or **Microsoft Foundry** as the kind
9. Select your subscription → Foundry resource → `text-embedding-3-small` deployment
10. Authentication: **System assigned identity**
11. Check the billing acknowledgment
12. Click **Next**

**Vectorize and enrich your images:**
13. Skip this step (or enable if your docs have meaningful images)
14. Click **Next**

**Advanced settings:**
15. ✅ **Enable semantic ranking** — this replaces the LLM-based reranker
16. Review the auto-generated index name (e.g., `vector-XXXXXXXXX`)
17. Optionally rename to something memorable: `rag-chunks`
18. Click **Next** → **Submit**

#### Step 1.5 — Verify the Index

1. Go to **Azure AI Search → Indexes** in the left menu
2. Click on your new index (e.g., `rag-chunks`)
3. Note the **Document count** — should be > 0 after the indexer runs
4. Click **Search explorer**
5. Test a query: type `"risk factors"` and click **Search**
6. Verify results contain relevant chunks from your uploaded document

✅ **Phase 1 complete** — your knowledge base is indexed and searchable.

---

### Phase 2 — Create Foundry Prompt Agents

**Goal:** Define each pipeline stage as a Foundry Prompt Agent with specific instructions and model assignments.

#### Step 2.1 — Connect AI Search to Your Foundry Project

1. Go to [ai.azure.com](https://ai.azure.com) → your project
2. Go to **Management → Connected resources** (or **Settings → Connections**)
3. Click **+ New connection** → **Azure AI Search**
4. Select your AI Search resource (`search-foundry-rag`)
5. Name the connection: `rag-search-connection`
6. Authentication: **Microsoft Entra ID** (recommended) or **API Key**
7. Click **Create**

#### Step 2.2 — Create the Planner Agent

1. In Foundry portal → **Build** → **Agents** → **+ New agent**
2. Configure:
   - **Name**: `RAG-Planner`
   - **Model**: `gpt-4o`
   - **Instructions**:
     ```
     You are a research planner. Given a user's question, decompose it into 2-5 
     ordered sub-questions that together will fully answer the original question.
     
     For each step, assign a tool:
     - "search_docs" — for questions answerable from the indexed knowledge base
     - "search_web" — for questions requiring current/live information
     
     The knowledge base contains: {describe your indexed documents here, e.g., 
     "NVIDIA's 2024 Annual Report (10-K filing) covering financials, risk factors, 
     business operations, and market analysis."}
     
     Respond with JSON only:
     {
       "steps": [
         {
           "subQuestion": "What are NVIDIA's primary revenue segments?",
           "reasoning": "Need to understand revenue breakdown before analyzing risks",
           "tool": "search_docs"
         }
       ]
     }
     ```
   - **Output format**: JSON Schema
   - **Tools**: None (planning only)
3. Click **Save**

#### Step 2.3 — Create the Query Rewriter Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-QueryRewriter`
   - **Model**: `gpt-4o-mini`
   - **Instructions**:
     ```
     You are a search query optimizer. Given a sub-question and any prior research 
     findings, rewrite the sub-question into a precise, targeted search query that 
     will retrieve the most relevant results.
     
     Rules:
     - Use specific terminology that matches document language
     - Include key entities, numbers, or section names when relevant
     - If prior findings exist, use them to refine the query
     - Output only the rewritten query text, nothing else
     ```
   - **Tools**: None
3. Click **Save**

#### Step 2.4 — Create the Search Agent (with AI Search + Web Search tools)

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-Search`
   - **Model**: `gpt-4o-mini`
   - **Instructions**:
     ```
     You are a research assistant. Search for information to answer the given 
     question. Use the Azure AI Search tool for internal document queries and 
     the Web Search tool for current/live information.
     
     Always cite your sources with URLs when available.
     Return the most relevant passages you find.
     ```
   - **Tools**:
     - **Azure AI Search**: 
       - Connection: `rag-search-connection`
       - Index: `rag-chunks`
       - Query type: `vector_semantic_hybrid`
       - Top K: `10`
     - **Web Search**: (add for web grounding)
3. Click **Save**

#### Step 2.5 — Create the Distiller Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-Distiller`
   - **Model**: `gpt-4o-mini`
   - **Instructions**:
     ```
     You are an evidence distiller. Given retrieved search results, compress them 
     into a single dense paragraph of no more 300 words.
     
     Rules:
     - Preserve exact facts, numbers, dates, and named entities
     - Include inline citations [Source: document name]
     - Remove redundancy across overlapping passages
     - Focus only on information relevant to the question
     - Output only the distilled paragraph, nothing else
     ```
   - **Tools**: None
3. Click **Save**

#### Step 2.6 — Create the Reflection Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-Reflection`
   - **Model**: `gpt-4o-mini`
   - **Instructions**:
     ```
     You are a research note-taker. Given distilled evidence from a research step, 
     write a single factual sentence summarizing what was learned.
     
     This sentence will be added to a running research history to track progress.
     Be specific and factual. Output only the one-sentence summary.
     ```
   - **Tools**: None
3. Click **Save**

#### Step 2.7 — Create the Policy Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-Policy`
   - **Model**: `gpt-4o`
   - **Instructions**:
     ```
     You are a research policy controller. Given:
     - The original research plan (list of sub-questions)
     - Completed research steps and findings so far
     - The current step index
     
     Decide whether to:
     - CONTINUE — more sub-questions remain and research is on track
     - FINISH — enough evidence has been gathered to answer the original question,
       OR all plan steps are exhausted
     
     Respond with JSON only:
     { "action": "CONTINUE" }
     or
     { "action": "FINISH" }
     ```
   - **Output format**: JSON Schema
   - **Tools**: None
3. Click **Save**

#### Step 2.8 — Create the Synthesis Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-Synthesis`
   - **Model**: `gpt-4o`
   - **Instructions**:
     ```
     You are a research synthesizer. Given:
     - The original user question
     - All accumulated research evidence (distilled contexts)
     - The research history (one-sentence summaries per step)
     
     Write a comprehensive, well-structured answer that:
     - Directly addresses the original question
     - Synthesizes information across all research steps
     - Includes inline citations [Source: document name] or [Source: URL]
     - Is thorough but concise — aim for 300-500 words
     - Uses clear structure (paragraphs, bullet points where helpful)
     ```
   - **Tools**: None
3. Click **Save**

#### Step 2.9 — Create the One-Shot Answer Agent

1. **+ New agent**
2. Configure:
   - **Name**: `RAG-OneShotAnswer`
   - **Model**: `gpt-4o`
   - **Instructions**:
     ```
     You are a helpful assistant. Answer the user's question using the Azure AI 
     Search tool to find relevant information from the knowledge base.
     
     Always cite your sources. Provide a clear, comprehensive answer.
     ```
   - **Tools**:
     - **Azure AI Search**:
       - Connection: `rag-search-connection`
       - Index: `rag-chunks`
       - Query type: `vector_semantic_hybrid`
       - Top K: `5`
3. Click **Save**

✅ **Phase 2 complete** — all 8 agents are defined.

---

### Phase 3 — Create Foundry Toolbox

**Goal:** Bundle AI Search and Web Search into a single managed toolbox.

#### Step 3.1 — Create the Toolbox

1. In Foundry portal → install the **Microsoft Foundry Toolkit** VS Code extension (or use portal)
2. Go to **Tools** → **+ Add Toolbox**
3. Configure:
   - **Name**: `rag-tools`
   - **Description**: "RAG pipeline tools — AI Search + Web Search"
4. Add tools:
   - **Azure AI Search**:
     - Connection: `rag-search-connection`
     - Index: `rag-chunks`
     - Query type: `vector_semantic_hybrid`
     - Top K: `10`
   - **Web Search**: (no extra config needed)
5. Click **Publish**

#### Step 3.2 — Verify the Toolbox

1. Copy the toolbox **MCP endpoint URL** from the Tools view
2. The endpoint follows this pattern:
   ```
   {project_endpoint}/toolboxes/rag-tools/mcp?api-version=v1
   ```
3. You can now attach this toolbox to any agent instead of configuring tools individually

✅ **Phase 3 complete** — tools are centrally managed.

---

### Phase 4 — Build One-Shot Workflow

**Goal:** Build the simplest pipeline first to validate that tools, agents, and workflow all work together.

#### Step 4.1 — Create the Workflow

1. In Foundry portal → **Build** → **Create new workflow** → **Sequential**
2. Name it: `OneShot-RAG`

#### Step 4.2 — Add Nodes

Add the following nodes in order:

**Node 1 — Ask Question:**
1. Click **+** → **Ask a question**
2. Message: `"What would you like to know?"`
3. Save response as variable: `$userQuery`

**Node 2 — Invoke One-Shot Answer Agent:**
1. Click **+** → **Invoke agent**
2. Select existing agent: `RAG-OneShotAnswer`
3. Input: `$userQuery`
4. Save output as: `$answer`

**Node 3 — Send Message:**
1. Click **+** → **Send message**
2. Message: `$answer`

#### Step 4.3 — Test the Workflow

1. Click **Save**
2. Click **Run Workflow**
3. In the chat window, ask a question about your indexed document
   - Example: `"What were NVIDIA's total revenues in fiscal 2024?"`
4. Verify:
   - The agent searches the AI Search index
   - The response includes relevant information with citations
   - The answer is accurate based on your source document

✅ **Phase 4 complete** — One-Shot pipeline works end-to-end.

---

### Phase 5 — Build Deep-Thinking Workflow

**Goal:** Build the full agentic pipeline with planning, looping, and policy-driven control flow.

#### Step 5.1 — Create the Workflow

1. In Foundry portal → **Build** → **Create new workflow** → **Sequential**
2. Name it: `Agentic-RAG`

#### Step 5.2 — Add Nodes

Build the following node sequence:

**Node 1 — Ask Question:**
1. Click **+** → **Ask a question**
2. Message: `"What would you like to research?"`
3. Save response as: `$userQuery`

**Node 2 — Invoke Planner:**
1. Click **+** → **Invoke agent** → select `RAG-Planner`
2. Input: `$userQuery`
3. In **Action settings** → **Save output as** → create variable `$plan` (JSON)

**Node 3 — Set Variables:**
1. Click **+** → **Set variable**
2. Set `$stepIndex` = `0`
3. Add another **Set variable**: `$researchHistory` = `""`
4. Add another **Set variable**: `$distilledContexts` = `""`

**Node 4 — For Each Loop (research steps):**
1. Click **+** → **For each**
2. Loop over: `$plan.steps`
3. Current item variable: `$currentStep`

Inside the loop, add these nodes:

**Node 4a — Invoke Query Rewriter:**
1. Click **+** → **Invoke agent** → select `RAG-QueryRewriter`
2. Input: `Concat("Sub-question: ", $currentStep.subQuestion, "\n\nPrior findings: ", $researchHistory)`
3. Save output as: `$rewrittenQuery`

**Node 4b — If/Else (tool routing):**
1. Click **+** → **If/Else**
2. Condition: `$currentStep.tool = "search_docs"`
3. **If true** → **Invoke agent** → `RAG-Search`
   - Input: `$rewrittenQuery`
   - Save output as: `$searchResults`
4. **If false** → **Invoke agent** → `RAG-Search` (same agent, but will use web search)
   - Input: `Concat("Search the web for: ", $rewrittenQuery)`
   - Save output as: `$searchResults`

**Node 4c — Invoke Distiller:**
1. Click **+** → **Invoke agent** → select `RAG-Distiller`
2. Input: `Concat("Question: ", $currentStep.subQuestion, "\n\nEvidence:\n", $searchResults)`
3. Save output as: `$distilledContext`

**Node 4d — Update distilled contexts:**
1. Click **+** → **Set variable**
2. `$distilledContexts` = `Concat($distilledContexts, "\n\n---\nStep ", Text($stepIndex), ": ", $distilledContext)`

**Node 4e — Invoke Reflection:**
1. Click **+** → **Invoke agent** → select `RAG-Reflection`
2. Input: `Concat("Question: ", $currentStep.subQuestion, "\n\nDistilled evidence: ", $distilledContext)`
3. Save output as: `$reflection`

**Node 4f — Update research history:**
1. Click **+** → **Set variable**
2. `$researchHistory` = `Concat($researchHistory, "\n- Step ", Text($stepIndex), ": ", $reflection)`

**Node 4g — Invoke Policy:**
1. Click **+** → **Invoke agent** → select `RAG-Policy`
2. Input:
   ```
   Concat("Original question: ", $userQuery,
          "\n\nResearch plan: ", $plan,
          "\n\nCompleted research:\n", $researchHistory,
          "\n\nCurrent step: ", Text($stepIndex), " of ", Text(CountRows($plan.steps)))
   ```
3. Save output as: `$decision` (JSON)

**Node 4h — If/Else (continue or finish):**
1. Click **+** → **If/Else**
2. Condition: `$decision.action = "FINISH"`
3. **If true** → **Go to** → `Synthesis` node (Node 5)
4. **If false** → increment `$stepIndex`, continue loop

**Node 5 — Invoke Synthesis:**
1. Click **+** → **Invoke agent** → select `RAG-Synthesis`
2. Input:
   ```
   Concat("Original question: ", $userQuery,
          "\n\nAll research evidence:\n", $distilledContexts,
          "\n\nResearch history:\n", $researchHistory)
   ```
3. Save output as: `$finalAnswer`

**Node 6 — Send Message:**
1. Click **+** → **Send message**
2. Message: `$finalAnswer`

#### Step 5.3 — Save and Test

1. Click **Save**
2. Click **Run Workflow**
3. Ask a complex, multi-hop question:
   - Example: `"How do NVIDIA's supply chain risks relate to their competitive position, and what are they doing to mitigate those risks?"`
4. Verify:
   - Planner decomposes into multiple sub-questions
   - Each step searches and distills evidence
   - Policy decides when to finish
   - Synthesis produces a comprehensive, cited answer

#### Step 5.4 — Toggle YAML View (Optional)

1. Toggle **YAML Visualizer View** to **On**
2. Review the generated YAML — this gives you version-controlled, exportable workflow definition
3. Each **Save** creates a new immutable version with full history

✅ **Phase 5 complete** — Deep-Thinking pipeline works end-to-end.

---

### Phase 6 — Agent Identity & RBAC

**Goal:** Replace any API key usage with managed identity authentication.

#### Step 6.1 — Verify Project Managed Identity

1. In [Azure Portal](https://portal.azure.com), go to your Foundry resource
2. Go to **Resource Management → Identity**
3. Ensure **System-assigned managed identity** is **On**

#### Step 6.2 — Assign RBAC Roles

Assign the following roles to your **Foundry project's managed identity**:

**On the Azure AI Search resource:**
1. Go to AI Search → **Access control (IAM)** → **Add role assignment**
2. Assign: `Search Index Data Reader` → to Foundry managed identity
3. Assign: `Search Service Contributor` → to Foundry managed identity

**On the Azure OpenAI / Foundry resource:**
1. Go to Foundry resource → **Access control (IAM)** → **Add role assignment**
2. Assign: `Azure AI User` → to Foundry managed identity

**On the Storage account:**
1. Go to Storage → **Access control (IAM)** → **Add role assignment**
2. Assign: `Storage Blob Data Reader` → to Foundry managed identity (if not already done in Phase 1)

#### Step 6.3 — Remove API Keys

1. In your AI Search connections, switch authentication from **API Key** to **Microsoft Entra ID**
2. In your Foundry agent tool configurations, verify they use managed identity
3. Delete any stored API keys from connection settings

✅ **Phase 6 complete** — zero secrets, all RBAC-based.

---

### Phase 7 — Memory (Optional)

**Goal:** Add persistent cross-session memory for personalized experiences.

#### Step 7.1 — Create Memory Store

1. In Foundry portal → **Management** (or via SDK)
2. Create a memory store:
   - **Name**: `rag-memory`
   - **Chat model**: `gpt-4o`
   - **Embedding model**: `text-embedding-3-small`
   - **Options**: Enable user profile + chat summary
   - **User profile details**: `"Remember user's research topics, preferred document sources, and question patterns"`

#### Step 7.2 — Attach to Agents

1. Edit agents that should have memory (e.g., `RAG-Synthesis`, `RAG-OneShotAnswer`)
2. Add the **Memory Search** tool:
   - **Memory store**: `rag-memory`
   - **Scope**: `{{$userId}}` (auto per-user isolation)
   - **Update delay**: `300` seconds (5 min inactivity)

#### Step 7.3 — Test Cross-Session Recall

1. Run a query in the workflow: `"What are NVIDIA's risk factors?"`
2. Wait for memory to be stored (~5 min)
3. Start a **new conversation**
4. Ask: `"Based on what I researched before, what else should I look into?"`
5. Verify the agent recalls your prior research topic

✅ **Phase 7 complete** — agents remember across sessions.

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
