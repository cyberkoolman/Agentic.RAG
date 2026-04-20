# Agentic Deep-Thinking RAG Pipeline — Presentation Script

### Speaker: Randy Park | Sr. Cloud Solution Architect

---

## Slide 1 — Building an Agent Deep-Thinking RAG Pipeline

*Hi everyone. I'm Randy Park, a Cloud Solution Architect at Microsoft. Today we're going to build something real together — an Agentic Deep-Thinking RAG pipeline. Not just talk about it theoretically, but walk through every phase, every design decision, and every component that makes it actually work.*

*By the end of this session, you'll have a clear mental model of why standard RAG breaks down on complex queries, and exactly what architecture changes you need to fix that. Let's get into it.*

### 📌 Glossary — Slide 1

| Term | What it means |
|---|---|
| **RAG** | Retrieval-Augmented Generation — a technique where an AI model searches for relevant documents before generating an answer, so it doesn't have to rely solely on memorized training data |
| **Agentic** | A system that can make decisions, plan, loop, and revise its own behavior — rather than just executing a fixed sequence of steps |
| **Deep-Thinking** | The pipeline deliberates before answering: it breaks the question apart, researches each part, and reflects on what it found before deciding whether it knows enough |

---
---

## Slide 2 — Agenda

*Quick context before we dive in: this session is aimed at developers. I'm not here to turn you into a data scientist, and I'm certainly not here to brag in front of data scientists — because that's a fight we'd lose.*

*The real goal is practical. How do I take these new AI capabilities and actually wire them into enterprise applications? How do I replace something that's overly complex with something more elegant? And critically — how do I know when to use agentic approaches versus simpler ones?*

*We'll focus on benefits and how things work, not on the math under the hood. Three topics: what Agentic RAG actually is, why standard RAG fails on hard questions, and then the full Deep-Thinking pipeline phase by phase.*

### 📌 Glossary — Slide 2

| Term | What it means |
|---|---|
| **Standard RAG** | The basic version: embed a query, retrieve matching chunks, pass them to the LLM, get one answer — no planning, no looping |
| **Enterprise application** | Production software at organizational scale — where reliability, accuracy, and explainability actually matter |
| **Pipeline** | The ordered sequence of processing steps that transform a user's question into a final answer |

---
---

## Slide 3 — The Core Problem with Standard RAG

*Here's the honest truth about standard RAG: it doesn't fail because the language model is dumb. It fails because the architecture is too simple.*

*Look at the diagram on the right. One query goes in. One retrieval call happens. One LLM prompt gets sent. One answer comes out. The whole system is linear and stateless. Every complex question gets flattened into a single retrieval shot.*

*What can it not handle? Multi-hop reasoning — where you need to combine information from two different sources. Questions that require both your private documents and current web data. And the system has no concept of "I don't know enough yet" — it always gives you an answer, even when it shouldn't.*

*The fix is architectural. Not a better model — a better loop.*

### 📌 Glossary — Slide 3

| Term | What it means |
|---|---|
| **Multi-hop reasoning** | Questions that require connecting information from multiple places — like "How does NVIDIA's supply chain risk affect its 2024 stock guidance?" which requires understanding both the risk factors and the financial outlook |
| **Stateless** | The system has no memory between steps — each retrieval call starts fresh with no knowledge of what was already found |
| **Linear architecture** | One fixed sequence: query in → retrieve → answer out. No branching, no looping, no revision |

---
---

## Slide 4 — Deep-Thinking RAG Pipeline Overview

*So what does the fix look like? Six interconnected phases that replace that single linear shot.*

*Phase one is Plan: instead of sending the raw question to a search engine, we decompose it into structured sub-questions and assign each one a tool — search_docs for the internal document, search_web for anything that requires live data.*

*Phase two is Retrieve: a supervisor agent looks at each sub-question and picks the best search strategy — semantic, keyword, or a hybrid. It's not one-size-fits-all.*

*Phase three is Refine: we rewrite the sub-question into a precise search query before sending it, then compress the retrieved evidence down to only what matters.*

*Phase four is Reflect: whatever we found gets distilled into a single factual sentence and added to a shared memory object.*

*Phase five is Critique: a policy agent looks at the accumulated research and makes a strategic call — continue to the next sub-question, revise the plan, or finish.*

*Phase six is Synthesize: once the policy agent says finish, all the accumulated evidence gets merged into a comprehensive, citable final answer.*

*The key thing to notice is that arrow at the bottom — "re-loop if the policy agent decides to revise." This isn't a chain, it's a cycle. That cycle is what makes it agentic.*

*Let me make this concrete. Here's a real query going through the full loop — step by step.*

---

### 🔍 Use-Case Walkthrough — Jehovah's Witnesses US Branch Growth Analysis

**Scenario:** A US Branch coordinator loads the **annual congregation service reports** — publisher counts, Bible study totals, pioneer activity, and memorial attendance organized by territory and circuit — and asks:

**User query:**
> *"Based on our US Branch service reports, which territories show the highest Bible study growth over the past year, and what demographic, economic, or sociological factors might be contributing to that growth? Are there other US regions with a similar profile where we aren't seeing growth yet?"*

This question has three layers. The first two live entirely inside internal reports. The third requires the live web — Census data, economic indicators, sociological research — none of which any internal report could contain. And the final ask — *"where aren't we growing yet?"* — requires the pipeline to reason across all of it and surface something the user didn't already know. Standard RAG stops at layer one. This pipeline goes all the way through.

---

#### 🧠 Phase 1 — PLAN

*The planner reads the question and identifies five distinct research steps — more than usual, because this question has genuine depth:*

```
Step 1 → "Which US territories show the largest year-over-year increase
          in active Bible studies?"
          tool: search_docs  (service report data)

Step 2 → "What pioneer density and return-visit activity patterns appear
          in the top-growth territories?"
          tool: search_docs  (service report activity breakdown)

Step 3 → "What are the immigration and foreign-born population trends
          in those top-growth regions?"
          tool: search_web   (US Census Bureau data)

Step 4 → "What do economic stress indicators look like in those same
          regions — unemployment, housing costs, displacement rates?"
          tool: search_web   (Bureau of Labor Statistics, regional economic data)

Step 5 → "What does sociological research say about the relationship
          between immigration, economic stress, and religious community growth?"
          tool: search_web   (academic and demographic research)
```

*Five steps. Two tools. The planner correctly separates what's in the reports from what requires external research — and it sequences them logically: find the growth first, then explain it, then predict where else it should be happening.*

---

#### 📚 Phase 2 — RETRIEVE  *(Step 1)*

*Sub-question: "Which US territories show the largest year-over-year increase in active Bible studies?"*

*The retrieval supervisor selects **keyword search** — the service reports use precise terminology: "Bible studies," "active studies," "territory," "circuit." BM25 exact matching outperforms semantic search here because the vocabulary is already precise.*

*Result: top 10 candidate report sections, filtered by metadata to the **Annual Field Service Summary** section.*

---

#### ✂️ Phase 3 — REFINE  *(Step 1)*

**Query rewriter** sharpens the sub-question:

```
Before: "Which US territories show the largest year-over-year increase
         in active Bible studies?"
After:  "Bible study count increase year-over-year circuit territory
         US Branch annual service report highest growth 2023 2024"
```

**Distiller** compresses the top 3 report sections:

> *"South Florida (Miami Metro circuit), Houston TX South, and the Inland Empire CA circuits each show Bible study increases of 23–31% year-over-year — the three highest growth rates in the US Branch, outpacing the national average of 8% by a factor of 3 or more."*

---

#### 🪞 Phase 4 — REFLECT  *(Step 1)*

> **ResearchHistory[0]:** *"Top three US growth territories: South Florida (+31%), Houston TX South (+27%), Inland Empire CA (+23%) — all significantly above the 8% national average."*

---

#### ⚙️ Phase 5 — CRITIQUE  *(after Step 1)*

*Growth territories identified, but no explanatory factors yet. Four steps remaining.*

> **Decision: CONTINUE** → `StepSignal(1)`

---

*Step 2 loops through the same pipeline — this time pulling pioneer density and return-visit conversion data for those three territories. The supervisor switches to **hybrid search** — "pioneer-to-publisher ratio" is a conceptual measure that benefits from semantic understanding alongside exact terminology.*

*After Step 2:*

> **ResearchHistory[1]:** *"All three top-growth territories show pioneer-to-publisher ratios 40–60% above the national average, and return-visit-to-Bible-study conversion rates nearly double the US Branch mean — suggesting active field ministry is a proximate driver, but does not explain why these specific territories."*

> **Decision: CONTINUE** → `StepSignal(2)` — internal data exhausted, go external.

---

*Step 3 fires a Tavily web search targeting US Census immigration data for South Florida, Houston, and the Inland Empire.*

*After Step 3:*

> **ResearchHistory[2]:** *"All three territories saw foreign-born population growth of 18–24% between 2020–2024, driven primarily by immigration from Venezuela, Colombia, and Mexico — regions with existing familiarity with Jehovah's Witnesses and lower initial barriers to Bible study acceptance [Source: US Census Bureau, 2024]."*

---

*Step 4 queries economic stress indicators — unemployment, housing displacement, and cost-of-living pressure in those same three regions.*

*After Step 4:*

> **ResearchHistory[3]:** *"All three territories rank in the top quintile nationally for housing cost burden and immigrant economic displacement — conditions associated with elevated receptivity to community-based social support structures [Source: Bureau of Labor Statistics & Harvard Joint Center for Housing Studies, 2024]."*

---

*Step 5 pulls sociological research on religious growth dynamics in immigrant communities under economic stress.*

*After Step 5:*

> **ResearchHistory[4]:** *"Peer-reviewed research consistently identifies economic displacement and immigration transition as two of the strongest predictors of religious community growth — particularly for faiths offering structured social support, consistent visitation, and a sense of belonging across language barriers [Source: Pew Research, Journal of Religion and Demography, 2023–2024]."*

---

#### ⚙️ Phase 5 — CRITIQUE  *(after Step 5)*

*All five steps complete. Internal data established the growth. External research established three converging factors: recent immigration from high-familiarity countries, economic stress, and active pioneer presence. The pattern is clear — the final question, "where else?", can now be answered.*

> **Decision: FINISH** → `FinishSignal` sent to Synthesis.

---

#### 💡 Phase 6 — SYNTHESIZE

*The synthesis agent reads all five distilled contexts, all five reflection summaries, and the original question — and produces:*

> *"Your top three Bible study growth territories — South Florida (+31%), Houston TX South (+27%), and Inland Empire CA (+23%) — share three converging conditions: above-average pioneer density, significant recent immigration from Latin America (18–24% foreign-born population growth since 2020), and elevated economic stress including housing displacement and income pressure [Sources: US Branch Service Report 2024; US Census Bureau; BLS].*
>
> *Sociological research identifies these same three factors — active community visitation, immigrant transition, and economic displacement — as the strongest predictors of religious community growth [Source: Pew Research, 2024].*
>
> *Two US regions currently match this profile but show below-average Bible study activity: **Chicago Southwest suburbs** (large, fast-growing Venezuelan and Colombian immigrant population, rising housing cost burden, 30% below national pioneer density average) and **Charlotte NC metro** (one of the fastest-growing Latin American immigrant concentrations in the Southeast, high economic transition indicators, limited Spanish-language congregation coverage). These represent the highest-potential untapped territories in the current US Branch based on conditions already demonstrated to drive growth elsewhere."*

*That final paragraph is the answer the user didn't know they needed. The pipeline didn't just read the reports — it connected internal performance data to external demographic and economic research, found the pattern, and turned the pattern into a forward-looking recommendation. No single lookup could have produced that. Only the loop could.*

---

*Five steps. Two tools. One insight that changes where the Branch focuses next. That's what the agentic loop does that a search box never will.*

### 📌 Glossary — Slide 4

| Term | What it means |
|---|---|
| **Sub-question** | A smaller, focused question that is one piece of the larger multi-hop question — the planner breaks the original query into several of these |
| **Tool selection** | The planning agent's decision about which search method to use for each sub-question: internal document search vs. live web search |
| **Policy agent** | The control-flow decision maker that decides whether the research loop should continue, revise its plan, or stop and synthesize |
| **Synthesize** | The final step where all accumulated evidence from every research step is combined into one coherent answer |
| **ResearchHistory** | The growing list of one-sentence findings accumulated in RAGState — one entry per completed research step |
| **REVISE** | A policy decision to rewrite the remaining research plan when the current approach has hit a dead end |

---
---

## Slide 5 — Phase 0 — Pre-Processing & Data Ingestion

*Before any query hits the pipeline, the documents need to be prepared. This is Phase Zero, and it's where a lot of the quality gets baked in.*

*First, fetch and clean. If you're loading an SEC filing or any raw HTML document, you strip the tags, normalize the whitespace, remove the legal boilerplate. Clean input means clean retrieval.*

*Second, chunking. We split the document into overlapping chunks — a thousand characters each, with 150 characters of overlap so we don't cut sentences in half. A typical 10-K produces around 380 chunks.*

*Third — and this is the important one — metadata-aware chunking. We run a regex over the document to detect section headers. Every chunk gets tagged with the section it came from: "Item 1A. Risk Factors," "Item 7. Management's Discussion," and so on.*

*Why does that matter? Because later in the pipeline, the planning agent can tell the retriever: "Only search chunks where the section is Item 1A." Instead of a keyword scatter-search across the whole document, you get a precise, scoped lookup. That's the difference between brute force and surgical precision.*

### 📌 Glossary — Slide 5

| Term | What it means |
|---|---|
| **Chunk** | A fixed-size segment of text cut from a document — small enough to fit into a retrieval call, but large enough to contain useful context |
| **Chunk overlap** | A small amount of repeated text between consecutive chunks so that information spanning a boundary doesn't get lost |
| **Metadata-aware chunking** | Attaching labels (like section names) to each chunk so the retriever can filter by those labels rather than searching blindly |
| **SEC 10-K** | An annual financial report that public companies file with the US Securities and Exchange Commission — commonly used as a RAG knowledge source |

---
---

## Slide 6 — Phase 1 — Strategic Planning Agent

*The planning agent is the brain of this pipeline. It doesn't search for anything itself — it decides what needs to be searched, in what order, and with which tool.*

*When a complex multi-hop question comes in, the planner reads it and identifies the sub-questions embedded inside. For a question like "What are NVIDIA's competitive risks and how has their stock moved since the 2023 annual report?" — that's actually two separate lookups. One is in the document. One requires the live web.*

*For each sub-question, the planner selects a tool. `search_docs` when the answer should be in the indexed knowledge base. `search_web` when the answer requires current information that no static document would have. The planner knows the difference — and that's critical, because sending a "what happened in 2024" query to a 2023 document is a guaranteed miss.*

*The planner outputs a structured Plan object — not a vague summary, but an ordered list of sub-questions with tool assignments that the rest of the pipeline executes step by step.*

*On the right you see the dual LLM strategy. The planner uses the most powerful reasoning model — planning is a high-stakes decision that deserves the best model. Routine sub-tasks like rewriting or summarizing use a faster, cheaper model. You pay for intelligence where it matters.*

### 📌 Glossary — Slide 6

| Term | What it means |
|---|---|
| **`search_docs`** | A tool assignment telling the pipeline to search the indexed internal knowledge base (documents you loaded) for this sub-question |
| **`search_web`** | A tool assignment telling the pipeline to search the live internet for this sub-question — used when the answer requires up-to-date information |
| **Dual LLM strategy** | Using two different language models in the same pipeline: a powerful model for high-stakes decisions (planning, synthesis), and a faster/cheaper model for routine tasks |
| **Plan object** | A structured data record containing the ordered list of sub-questions and tool assignments that the pipeline executes |
| **Reasoning model** | The higher-capability language model — used where quality of output matters most, like planning and final synthesis |

---
---

## Slide 7 — Phase 2 — Multi-Stage Retrieval Funnel

*Once the planner hands off a sub-question, the retrieval funnel kicks in. And the first decision it makes is: what kind of search should I run?*

*There are three strategies. Vector search uses embeddings to find semantically similar content — great for conceptual or paraphrased questions. Keyword search — BM25 — finds exact or fuzzy term matches — better for specific names, acronyms, or numeric values. Hybrid search fuses both using Reciprocal Rank Fusion.*

*The retrieval supervisor agent reads the sub-question and picks the right strategy. Why not always use hybrid? Because for factual lookups — "What was the revenue figure in fiscal Q3?" — keyword search is faster and more precise. Hybrid adds noise for questions that don't need semantic flexibility. The supervisor makes that call.*

*The result of whichever strategy runs is a broad recall set — the top 10 candidates. Those go into the Cross Encoder reranker. The cross encoder reads all 10 documents jointly with the sub-question and scores them for relevance. This is far more precise than the initial embedding distance, which scores each document independently. You come out with the top 3, high-precision documents.*

*Optionally — if section metadata was attached during ingestion — the supervisor can restrict the search scope to a specific section of the document. That's metadata filtering, and it dramatically cuts noise for structured documents like annual reports.*

### 📌 Glossary — Slide 7

| Term | What it means |
|---|---|
| **Vector search** | Finding documents based on meaning similarity — converts text to numbers (embeddings) and finds the closest matches |
| **BM25 keyword search** | A scoring algorithm that finds documents containing the exact or similar words in the query — good for precise term lookups |
| **Hybrid search (RRF)** | Combining vector and keyword results using Reciprocal Rank Fusion — a formula that merges two ranked lists into one |
| **Cross Encoder reranker** | A model that reads the query and each candidate document together (not separately) to score relevance — produces much higher precision than the initial search |
| **Retrieval Supervisor** | An internal agent that selects the search strategy (vector / keyword / hybrid) based on the type of sub-question |

---
---

## Slide 8 — Phase 3 — Refine: Query Rewriting & Contextual Distillation

*Phase 3 is about two things: improving what goes into retrieval, and improving what comes out of it.*

*On the input side: query rewriting. The problem is that a planning agent produces sub-questions written for a human to read — things like "What are the risks?" Those are terrible search queries. A vector database has no idea what to retrieve for something that vague.*

*The query rewriter takes each sub-question and rewrites it into something that retrieval engines can act on precisely. "What are the risks?" becomes "NVIDIA competitive risks semiconductor supply chain 2023." Specific. Keyword-rich. Aligned to the vocabulary actually present in the document. This is the single biggest driver of retrieval quality.*

*On the output side: contextual distillation. After retrieval, the top 3 chunks often have noise, repetition, and padding that wastes the LLM's context window. The distiller agent reads all three and compresses them into a single dense paragraph — keeping every fact and citation, stripping everything redundant.*

*The net result: better inputs into the LLM, fewer tokens consumed, higher reasoning precision. Both sides of the retrieval step are actively improved.*

### 📌 Glossary — Slide 8

| Term | What it means |
|---|---|
| **Query rewriting** | Transforming a natural language sub-question into an optimized search query — more specific, keyword-rich, and aligned to document vocabulary |
| **Contextual distillation** | Compressing the top retrieved documents into a single dense, noise-free paragraph while preserving all key facts and citations |
| **Context window** | The maximum amount of text a language model can process at once — wasting it on redundant content reduces the quality of reasoning |
| **Token cost** | The number of text units (tokens) sent to the LLM — directly determines API cost and processing speed |

---
---

## Slide 9 — Phases 4 & 5 — Reflect & Critique: The Agent's Memory & Control

*This is the heart of what makes this pipeline truly agentic — not just a smarter chain, but a thinking cycle.*

*Phase 4 is Reflection. After every research step, the reflection agent compresses the distilled context down to one factual sentence and appends it to the RAGState object. RAGState is the shared working memory of the entire pipeline. It tracks the original query, the full research plan, where we are in that plan, the accumulated findings from every step, and a slot for the final answer. Think of it like a researcher's notepad — building up knowledge incrementally rather than throwing it away between steps.*

*Phase 5 is Critique. After each reflection, the policy agent reads everything that's been accumulated and makes a strategic decision. Three options: CONTINUE — move to the next planned step. REVISE — the current approach has hit a dead end, rewrite the remaining plan. FINISH — we have sufficient evidence, go synthesize.*

*This loop is what separates this system from a chain. A chain always executes every step in order. This system can stop early, loop back, and change direction. That's agentic behavior.*

### 📌 Glossary — Slide 9

| Term | What it means |
|---|---|
| **RAGState** | The shared memory object that carries the query, research plan, accumulated findings, current step index, and final answer slot across every agent in the pipeline |
| **Reflection agent** | The agent that converts each step's distilled evidence into a single factual sentence and stores it in RAGState |
| **Policy agent** | The control-flow decision maker: reads accumulated research and decides whether to continue, revise the plan, or finish |
| **Agent chain vs. agent loop** | A chain always runs the same fixed steps in order; a loop can branch, repeat, or exit early based on what was found |

---
---

## Slide 10 — Phase 6 — Web Augmentation & Final Synthesis

*When the planning agent assigns a sub-question to `search_web`, the web search agent fires a live internet search — we're using Tavily, but SerpAPI works too. The results come back, go through the exact same reranker and distiller pipeline as document chunks, and get stored in RAGState alongside the document evidence.*

*This is important: web results are not a special case. They're first-class citizens in the evidence store. The synthesis agent doesn't know or care whether a piece of evidence came from a PDF you uploaded or from a live Google result — it all looks the same.*

*Once the policy agent calls FINISH, the synthesis agent receives the complete RAGState — every distilled context, every one-sentence reflection summary, and the original question. It uses the most powerful reasoning model. It merges everything into a single structured answer with citations back to the original sources.*

*This is not just an answer. It's a cited, reasoned synthesis drawing from both proprietary documents and the live web. That's something standard RAG fundamentally cannot produce.*

### 📌 Glossary — Slide 10

| Term | What it means |
|---|---|
| **Web augmentation** | Extending the knowledge base with live internet results for sub-questions that cannot be answered from static documents |
| **Tavily** | A search API optimized for LLM applications — returns clean, structured web results without needing to scrape raw HTML |
| **Synthesis agent** | The terminal agent that receives all accumulated research from RAGState and produces the final, comprehensive, citable answer |
| **Inline citation** | A reference embedded directly in the answer text pointing back to the original source document or URL |

---
---

## Slide 11 — Evaluation Framework — 3-Dimensional Quality Assessment

*Building a better pipeline only matters if you can measure the improvement. We evaluate on three dimensions.*

*Dimension one: qualitative, using an LLM as a judge. Four metrics — Faithfulness: is every claim in the answer grounded in retrieved evidence, or did the model hallucinate? Relevance: does the answer actually address what was asked? Soundness: is the reasoning coherent, or are there logical gaps? Depth: does it go beyond surface-level facts to provide real insight?*

*Dimension two: quantitative retrieval metrics. Precision — of the chunks retrieved, what fraction were actually relevant? Recall — of all the relevant chunks that exist in the knowledge base, what fraction did we find? And MRR, Mean Reciprocal Rank, which measures how high up in the ranked list the first relevant result appeared. These are measured against human-labeled ground truth.*

*Dimension three: performance. Total wall-clock latency per query. Total tokens consumed across the whole pipeline. And a comparison against a baseline simple RAG system — so you know exactly what the agentic overhead costs you in time and money, and what you get back in quality.*

*All three together give you a complete picture. Qualitative alone tells you it sounds good but not whether it's accurate. Quantitative alone doesn't tell you if the answer makes sense. Performance alone doesn't tell you if it's worth the cost.*

### 📌 Glossary — Slide 11

| Term | What it means |
|---|---|
| **LLM-as-Judge** | Using a language model to score answer quality on criteria like faithfulness and relevance — a scalable alternative to human evaluation |
| **Faithfulness** | Whether every statement in the generated answer is directly supported by the retrieved evidence — unfaithful answers contain hallucinations |
| **Precision (retrieval)** | The fraction of retrieved chunks that were actually relevant — high precision means less noise in the context window |
| **Recall (retrieval)** | The fraction of all relevant chunks that were successfully found — high recall means nothing important was missed |
| **MRR** | Mean Reciprocal Rank — measures how quickly the retriever surfaces the first relevant result; higher is better |
| **Hallucination** | When a model generates a confident-sounding statement that is not supported by any retrieved evidence |

---
---

## Slide 12 — Basic RAG vs. Deep-Thinking Agentic RAG

*Let me make the architectural differences concrete with a side-by-side comparison.*

*Start at the top: query decomposition. Basic RAG sends the raw query straight to the search engine — no analysis, no planning. Agentic RAG produces a structured multi-step research plan before anything gets retrieved.*

*Retrieval strategy: Basic RAG uses a fixed method — whatever was configured at setup time. Agentic RAG uses a supervisor that dynamically selects the best strategy per sub-query.*

*Query optimization: Basic RAG sends your exact words to the retriever. Agentic RAG rewrites the sub-question into a precision search query tailored to the document's vocabulary.*

*Result precision: Basic RAG gives you top-K by similarity score. Agentic RAG runs a cross encoder that scores all candidates jointly with the query — you get top-3 with genuinely high relevance.*

*Context compression: Basic RAG stuffs raw chunks directly into the LLM prompt. Agentic RAG distills them first.*

*Memory: Basic RAG is stateless. Every query starts from zero. Agentic RAG maintains a persistent RAGState across all steps.*

*Revision ability: Basic RAG gets one shot. Agentic RAG can revise its plan mid-research based on what it found.*

*External knowledge: Basic RAG is limited to your indexed documents. Agentic RAG combines your documents with live web results.*

*Bottom line: for simple factual lookups, Basic RAG is fast and cheap and fine. For complex multi-hop questions — the ones that actually matter in enterprise use cases — only the agentic approach produces a complete, citable answer.*

### 📌 Glossary — Slide 12

| Term | What it means |
|---|---|
| **Basic RAG** | Standard single-pass retrieval: embed query → retrieve chunks → answer. No planning, no revision, no memory |
| **One-Shot** | The same as Basic RAG — a single attempt at retrieval and generation with no looping or iteration |
| **Multi-hop question** | A question that requires connecting information from more than one source or document section to produce a complete answer |
| **Cross encoder** | A reranking model that reads the query and each candidate document together in a single pass — more accurate than comparing them independently |

---
---

## Slide 13 — Key Takeaways

*Let me leave you with six things worth remembering.*

*One: architecture over intelligence. When RAG fails, the instinct is to blame the model. Almost every time, the real problem is architectural — the pipeline is too simple. Agentic loops are the fix.*

*Two: think in cycles, not lines. Plan, retrieve, refine, reflect, critique, synthesize — and repeat until confident. That's how humans research. That's how the pipeline should work.*

*Three: metadata is a superpower. Tagging chunks with section context turns a scatter-search across thousands of chunks into a targeted lookup in exactly the right section. Don't skip this during ingestion.*

*Four: state is everything. RAGState gives the agent memory. Without cumulative history, there's no true reasoning — just the same one-shot pattern running multiple times.*

*Five: the dual LLM strategy saves real money at scale. Use your most powerful model for planning and synthesis. Use a fast cheap model for rewriting, reranking, and distillation. The performance impact is minimal. The cost impact is significant.*

*Six: measure all three dimensions. Qualitative LLM judgment, quantitative retrieval metrics, and performance benchmarks. Any one of those alone gives you an incomplete picture of how well the system is actually working.*

*Thank you. I'm happy to take questions.*

### 📌 Glossary — Slide 13

| Term | What it means |
|---|---|
| **Agentic loop** | The cyclical pipeline pattern: the system iterates — plans, retrieves, reflects, critiques — until it has sufficient evidence, rather than running once and stopping |
| **Cumulative history** | The growing record of everything the pipeline has found across all research steps — stored in RAGState and used by the policy agent and synthesis agent |
| **Cost per query** | The total LLM token spend for a single user query — including all agent calls across every pipeline step |
