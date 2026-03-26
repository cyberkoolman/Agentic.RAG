# Presentation Speech — Agentic Deep-Thinking RAG Pipeline
### Speaker: Randy Park | Sr. Cloud Solution Architect

---

> **How to use this script:**
> Each slide section has the speech text followed by a 📌 **Glossary** block that explains any new terms introduced on that slide. Read naturally — these are not rigid scripts, feel free to rephrase on the fly.

---

## Slide 1 — Title

*"Good [morning / afternoon], everyone. My name is Randy Park — I'm a Senior Cloud Solution Architect. Today we're going to talk about something that I think is going to genuinely change how you think about building AI applications.*

*The topic is Agentic Deep-Thinking RAG Pipelines. That's a mouthful, I know — but by the end of this session, every word in that title is going to make complete sense to you, and you'll understand exactly why each part matters.*

*Let's get into it."*

---

### 📌 Glossary — Slide 1

| Term | What it means |
|---|---|
| **RAG** | Retrieval-Augmented Generation. A technique where an AI doesn't just rely on what it was trained on — it first *retrieves* relevant information from your documents or databases, and then *generates* an answer using that retrieved context. Think of it as giving the AI a textbook to look at before answering your question. |
| **Agentic** | Describes an AI system that can take autonomous actions, make decisions, and loop through steps — like an agent working a task — rather than just responding once and stopping. |
| **Pipeline** | A series of connected processing steps where the output of one step feeds into the next. |

---

---

## Slide 2 — Agenda

*"Before we dive in, let me quickly walk you through what we're covering today.*

*We'll start with the big question — what even is Agentic RAG, and why does it exist? Then we'll look at what's fundamentally broken with the standard RAG approach that most people are building today.*

*From there, we'll walk through the Deep Thinking RAG pipeline, phase by phase — pre-processing, planning, retrieval, refining, reflecting, critiquing, and finally web augmentation. And we'll close with how to evaluate whether your pipeline is actually working.*

*One important thing before we start — and I want to be upfront about this — this session is geared toward developers. The goal is not to turn you into a data scientist. We're not going to go deep into the math or the theory. What we ARE going to focus on is: how do I take these new capabilities and infuse them into real enterprise applications? Can we take something that used to require a really complicated system, and build something more elegant and simpler? That is the intent here. We'll focus on the benefit and the how — not so much on what's happening under the hood."*

---

### 📌 Glossary — Slide 2

| Term | What it means |
|---|---|
| **Standard RAG** | The basic, most common form of RAG — the user asks a question, the system retrieves a few chunks of text, and the LLM generates one answer. Simple, but limited. |
| **Phase** | A distinct stage in the pipeline. Each phase does one specific job before handing off to the next. |
| **LLM** | Large Language Model. The AI model that reads context and generates text — for example, GPT-4, Claude, or Gemini. |

---

---

## Slide 3 — The Core Problem with Standard RAG

*"Alright, let's start with the problem. Why isn't the basic RAG approach enough?*

*Here's the key insight — and this is worth really internalizing: a RAG system fails not because the language model is dumb. The LLM is actually plenty smart. The problem is that the architecture surrounding it is too simple.*

*Think about what basic RAG actually does. The user sends a query. The system retrieves some chunks of text. It hands them to the LLM. The LLM generates an answer. Done. One shot, no memory, no ability to revise.*

*Now think about a complex question — something like: 'What were NVIDIA's biggest business risks in 2023, and how do those compare to what analysts are saying about them in the news today?' That question has multiple parts. Part of the answer lives in a local document. Part of it requires current news from the internet. And to really answer it well, you need to reason across both.*

*Basic RAG cannot do that. It retrieves once, generates once, and calls it a day. It's a linear, one-shot process trying to solve a cyclical, multi-step problem. That's the mismatch we're here to fix."*

---

### 📌 Glossary — Slide 3

| Term | What it means |
|---|---|
| **One-shot** | The system makes exactly one attempt — it retrieves once and generates once. No iteration, no self-correction. |
| **Multi-hop reasoning** | Answering a question that requires connecting information from multiple separate sources or steps. For example: "Who is the CEO of the company that acquired Activision?" requires knowing who acquired Activision first, then looking up that company's CEO. |
| **Linear pipeline** | A process that goes in one direction only — input → step 1 → step 2 → output. No loops, no going back. |
| **Cyclical process** | A process that may repeat steps based on what it finds, loop back, and refine its approach — like how a human researcher would. |

---

---

## Slide 4 — Deep-Thinking RAG Pipeline Overview

*"So what's the solution? The Deep-Thinking Agentic RAG Pipeline.*

*Instead of one shot, we break the process into six interconnected phases. Let me give you a quick map of all of them before we go deep on each one.*

*Phase one is PLAN — the agent takes your complex question and breaks it down into a structured, multi-step research plan. It figures out what sub-questions to ask and what tools to use.*

*Phase two is RETRIEVE — a supervisor agent dynamically picks the best search strategy for each sub-question. Not one-size-fits-all — it actually chooses.*

*Phase three is REFINE — we improve the search query before it hits the retrieval engine, and then we compress the results afterward so we're only feeding the model the signal it needs.*

*Phase four is REFLECT — after each step, the agent writes a one-sentence summary of what it found, and stores it in memory. It's building up a picture as it goes.*

*Phase five is CRITIQUE — a policy agent looks at everything gathered so far and makes a decision: keep going, revise the plan, or wrap up.*

*Phase six is SYNTHESIZE — once we have enough evidence, a final agent pulls everything together into one comprehensive, citable answer.*

*And notice this arrow here — if the policy agent decides to revise, the system loops back. That loop is the whole point. That's what makes this 'agentic'."*

---

### 📌 Glossary — Slide 4

| Term | What it means |
|---|---|
| **Agent** | A software component that can perceive its environment, make decisions, and take actions autonomously. In this context, each phase has its own specialized agent with a specific job. |
| **Sub-question** | A smaller, more specific question that, when answered, contributes a piece toward answering the larger complex query. |
| **RAG State** | A shared memory object that all agents read from and write to. It holds the original query, the plan, findings so far, and the final answer. Think of it as the agent's notepad. |
| **Policy agent** | An agent whose job is specifically to make control-flow decisions — should we continue, revise, or stop? |
| **Supervisor agent** | An agent that oversees and coordinates other agents or processes. In retrieval, it decides which search strategy to use. |

---

---

## Slide 5 — Phase 0: Pre-Processing & Data Ingestion

*"Before any of the smart stuff happens, we need to do the unglamorous but critical work — cleaning and preparing the data.*

*Phase zero is Pre-processing. This is about getting your raw documents into a shape that a retrieval system can actually work with effectively.*

*Step one: we download the raw document — in our example, an SEC 10-K filing — and we clean it. We strip out all the HTML tags, normalize the whitespace, collapse extra blank lines, and we end up with a clean text file. Straightforward.*

*Step two: we chunk it. We can't hand the LLM a 200-page document all at once — that would blow out the context window. So we split it into smaller chunks of around 1,000 characters each, with a 150-character overlap between adjacent chunks so we don't lose context at the boundaries. In our example, that gave us 378 chunks.*

*But here's where it gets interesting — step three, metadata-aware chunking. We don't just split the document blindly. We detect the section headers — things like 'Item 1A. Risk Factors' or 'Item 7. Management Discussion' — and we tag each chunk with the section it came from. So now every chunk knows where it lives in the document.*

*Why does that matter? Step four explains it. When the agent needs to find information about risks, it doesn't have to search all 381 chunks. It can say — 'only look in chunks tagged as Item 1A.' That turns a blunt keyword search into a surgical tool. That's the difference between production-grade RAG and a demo."*

---

### 📌 Glossary — Slide 5

| Term | What it means |
|---|---|
| **Chunking** | Splitting a large document into smaller pieces (chunks) that can be individually indexed and retrieved. |
| **chunk_size** | How many characters (or tokens) each chunk contains. Smaller chunks are more precise; larger chunks give more context. |
| **chunk_overlap** | The number of characters shared between adjacent chunks. Overlap prevents information from being cut off right at a boundary. |
| **Context window** | The maximum amount of text an LLM can read in one go. If you exceed it, the model can't process the input. |
| **Metadata** | Extra information attached to a piece of data that describes it. Here, metadata = the section name tagged onto each chunk. |
| **SEC 10-K** | An annual financial report that public companies in the US must file with the Securities and Exchange Commission. Used here as a realistic example of a complex enterprise document. |
| **BeautifulSoup** | A Python library used to parse and clean HTML — it strips out tags and extracts readable text. |
| **Regex** | Regular Expression — a pattern-matching tool used in code to find and extract specific text patterns, like section header formats. |

---

---

## Slide 6 — Phase 1: Strategic Planning Agent

*"Now we get to the brain of the operation — the Strategic Planning Agent.*

*When a complex query comes in, this agent is the first one to see it. And its job is not to answer the question — it's to figure out how to answer the question.*

*Let's say the user asks something like: 'What are NVIDIA's competitive risks in the semiconductor space, and what are analysts saying about them in 2024 news?' The planning agent looks at that and recognizes — okay, this has two parts. Part one is a question about a 2023 document I have. Part two requires live internet information that definitely isn't in a 2023 filing.*

*So it breaks it down into sub-questions, and for each sub-question, it picks the right tool. Search the local document — or search the web. It outputs a structured Plan object — not a vague idea, an actual structured list of steps with tool assignments. That plan is stored in the RAGState and drives everything that comes after.*

*Now — notice the LLM Strategy panel on the right. We use two different language models in this system, on purpose. The reasoning LLM is the most powerful and expensive model we have — and we only use it for the hard jobs: planning and final synthesis. The fast LLM is a smaller, cheaper, faster model for the routine sub-tasks. This is a deliberate cost optimization. You don't use a sledgehammer for everything."*

---

### 📌 Glossary — Slide 6

| Term | What it means |
|---|---|
| **Planning agent** | The agent responsible for decomposing a complex query into a step-by-step research plan. The "strategist" of the system. |
| **Tool selection** | The process of choosing which capability to use for a given sub-question — e.g., document search vs. web search. |
| **search_10k** | A tool that searches the local indexed document (in this case, the 10-K filing). |
| **search_web** | A tool that performs a live internet search to retrieve current information. |
| **Plan object** | A structured data object (like a list of steps) produced by the planning agent. Not prose — an actual structured output the code can iterate over. |
| **reasoning_llm** | The high-capability language model used for cognitively demanding tasks: planning and synthesis. Think GPT-4 or Claude Opus. |
| **fast_llm** | A smaller, faster, cheaper language model used for simpler tasks. Think GPT-3.5 or Claude Haiku. |
| **Dual LLM strategy** | Using two different models — one powerful, one cheap — and routing each task to the appropriate one to balance quality and cost. |

---

---

## Slide 7 — Phase 2: Multi-Stage Retrieval Funnel

*"Phase two is retrieval — but not the basic kind. This is a multi-stage funnel, and it's one of the most technically interesting parts of the whole system.*

*Here's the flow. The user's sub-query comes in. A Retrieval Supervisor Agent decides which search strategy to use — and there are three options: vector search, BM25 keyword search, or hybrid, which is a combination of both.*

*Why does the strategy choice matter? Because different question types work better with different retrieval methods. If you're asking 'what is the company's revenue model?' — a semantic vector search is great. If you're looking for an exact term like a product name or a regulation number — keyword search will find it faster and more accurately. The supervisor picks based on the nature of the sub-query.*

*Now — regardless of which search strategy runs, it retrieves the top 10 candidate chunks. But we don't send all 10 to the LLM. Instead, we run them through a CrossEncoder Reranker.*

*This is the precision step. The CrossEncoder looks at each of the 10 candidates together with your query — not separately, together — and scores them for actual relevance. Then we keep the top 3. So by the time we're done, we've gone from potentially hundreds of chunks in the index, down to 10 candidates, down to 3 high-precision results.*

*And one more thing — metadata filtering. Remember those section tags we added in pre-processing? The supervisor can tell the retriever: only search within this specific section. That's what turns a generic search into something surgical."*

---

### 📌 Glossary — Slide 7

| Term | What it means |
|---|---|
| **Vector Search** | A search method that finds documents by meaning/semantics, not exact words. Text is converted into numerical vectors (lists of numbers), and the search finds vectors that are mathematically "close" to the query vector. |
| **Embedding** | The numerical vector representation of a piece of text. Words or sentences with similar meanings have embeddings that are close together in vector space. |
| **BM25 Keyword Search** | A classic information retrieval algorithm that ranks documents based on how often query terms appear in them, with adjustments for document length. Best for exact or near-exact term matching. |
| **Hybrid Search** | A retrieval approach that combines both vector search and keyword search, then merges the results. |
| **RRF (Reciprocal Rank Fusion)** | A formula for merging ranked lists from multiple retrieval methods into one unified ranking. It gives credit to documents that rank well across multiple strategies. |
| **CrossEncoder Reranker** | A model that takes a (query, document) pair and scores how relevant the document is to the query — by reading both together. More accurate than vector similarity alone, but slower. |
| **Bi-encoder** | The model type used in vector search — it encodes the query and document separately, then compares. Faster than CrossEncoder but less precise. |
| **Top-K** | Retrieving the K most relevant results. Here: top-10 from retrieval, then top-3 after reranking. |
| **Metadata filtering** | Restricting a search to only chunks that match a specific metadata condition, like "section = Item 1A." |

---

---

## Slide 8 — Phase 3: Refine — Query Rewriting & Contextual Distillation

*"Phase three is about refinement — improving things both before and after retrieval.*

*Let's start with the before part — Query Rewriting. Here's the problem: the sub-questions the planning agent generates are written to be understood by a human. But a retrieval engine is not a human. If the agent's plan says 'find the risks,' that's too vague for a vector database or keyword search to work well.*

*The Query Rewriter Agent takes each sub-question and rewrites it into something specific and keyword-rich. So 'What are the risks?' becomes 'NVIDIA competitive risks semiconductor supply chain 2023.' That's a query a retrieval engine can actually act on. Small change, big difference in retrieval quality.*

*Now the after part — Contextual Distillation. Even after reranking, our top 3 chunks may contain repetition, irrelevant sentences, background context that wasn't asked for. If we hand all of that to the reasoning LLM, we're wasting tokens — which costs money — and we're diluting the signal, which hurts answer quality.*

*So a Distiller Agent reads the top 3 chunks and compresses them into one dense, signal-rich paragraph. It keeps only what's directly relevant to the sub-question and throws everything else out. The reasoning LLM then gets a clean, compact input — and it can focus all its capacity on reasoning, not on parsing noise."*

---

### 📌 Glossary — Slide 8

| Term | What it means |
|---|---|
| **Query Rewriting** | The process of taking a natural-language sub-question and rewriting it into a more precise, keyword-rich query optimized for retrieval engines. |
| **Contextual Distillation** | Compressing the retrieved chunks into a short, dense paragraph that contains only the information relevant to the current sub-question. |
| **Context window (revisited)** | Every LLM has a limit on how much text it can process at once. Distillation keeps the input small so the model can reason more effectively. |
| **Token** | The basic unit that LLMs process — roughly 3/4 of a word. LLM costs are typically measured per token, so reducing tokens reduces cost. |
| **Signal-to-noise ratio** | A metaphor for how much of the input is useful (signal) vs. irrelevant (noise). Distillation maximizes the signal. |

---

---

## Slide 9 — Phases 4 & 5: Reflect & Critique

*"We're now at the heart of what makes this system truly agentic — Phases 4 and 5, Reflect and Critique.*

*After each retrieval and distillation step, the Reflection Agent does something deceptively simple but incredibly powerful: it takes the distilled context and writes a single factual sentence summarizing what was found. That sentence gets appended to the RAGState in a field called past_steps.*

*So as the pipeline works through each sub-question, it's building up a running research history — a cumulative picture of everything it has learned so far. The agent isn't stateless anymore. It remembers.*

*The RAGState object tracks five things: the original query, the plan, those accumulated past steps, where in the plan we currently are, and a slot for the final answer when we're ready.*

*Now — Phase 5, Critique. After each reflection, the Policy Agent looks at the RAGState and makes one of three decisions:*

*CONTINUE — we have more steps in the plan to execute, keep going.*
*REVISE — something isn't working. We've hit a dead end, the information isn't there, or the plan needs to change. Loop back to planning and rewrite the remaining steps.*
*FINISH — we have enough evidence. Stop researching and generate the final answer.*

*That decision loop — reflect, critique, continue or revise or finish — is what makes this a thinking cycle, not just a chain. And that's the entire point of calling it agentic."*

---

### 📌 Glossary — Slide 9

| Term | What it means |
|---|---|
| **Reflection Agent** | The agent that summarizes each retrieval step into one factual sentence and appends it to the RAGState memory. |
| **RAGState** | The shared memory object for the entire pipeline session. It persists across all steps and is read and updated by every agent. |
| **past_steps** | The list field in RAGState where each reflection summary is stored. The full research history lives here. |
| **current_step_idx** | The index tracking which step of the plan the pipeline is currently executing. |
| **Policy Agent** | The agent that inspects RAGState after each reflection and decides: CONTINUE, REVISE, or FINISH. |
| **Stateless** | A system with no memory between steps — each step starts fresh with no knowledge of what came before. Basic RAG is stateless. |
| **Stateful** | A system that retains memory across steps. The Agentic RAG pipeline is stateful, thanks to RAGState. |
| **Control flow** | The logic that determines which step executes next — continue, revise, or finish. The Policy Agent owns control flow. |
| **Agentic loop** | The repeating cycle of: retrieve → reflect → critique → (continue or revise or finish). This loop is what separates agentic systems from simple chains. |

---

---

## Slide 10 — Phase 6: Web Augmentation & Final Synthesis

*"Phase 6 is where two powerful things happen — web augmentation and final synthesis.*

*Let's start with web augmentation. Remember in Phase 1, when the planning agent assigned the search_web tool to sub-questions that can't be answered by the local document? This is where that kicks in. When the pipeline hits a step that needs web search, it triggers a live internet search using a tool like Tavily or SerpAPI. The results come back, they go through the same distillation process as document chunks, and they get stored in RAGState alongside the document evidence.*

*This is powerful because it means your system isn't limited to what's in your database. If someone asks about something that happened last week, the system can go get it. The static document and the live web work together.*

*Now — synthesis. Once the Policy Agent says FINISH, all those accumulated past_steps in RAGState go to the Synthesis Agent. This agent uses the most powerful reasoning model we have. It reads every piece of evidence gathered across all the steps — from documents and from the web — and it produces one final, comprehensive answer. With citations. Not a vague summary — a structured, source-backed response.*

*The goal isn't just an answer. It's a reasoned, cited synthesis that draws from everything the system researched."*

---

### 📌 Glossary — Slide 10

| Term | What it means |
|---|---|
| **Web Augmentation** | Extending the pipeline's knowledge by pulling live information from the internet, not just from local documents. |
| **Tavily / SerpAPI** | Tools and APIs that perform web searches programmatically. Tavily is specifically designed for AI agent use cases. |
| **Synthesis Agent** | The final agent that reads all accumulated evidence and generates the comprehensive final answer. Uses the most powerful LLM. |
| **Citation** | A reference to the source of a piece of information — so the user knows where the answer came from and can verify it. |
| **Multi-source answer** | An answer that draws from multiple different sources — in this case, both internal documents and live web results. |

---

---

## Slide 11 — Evaluation Framework

*"One question that always comes up when you build something like this: how do you know it's actually working? That's where the evaluation framework comes in.*

*We measure quality across three completely different dimensions, and you need all three to get a real picture.*

*First: Qualitative evaluation. Here, an LLM acts as the judge. It reads the question, the retrieved evidence, and the generated answer, and it scores across four criteria: Faithfulness — is the answer actually grounded in the evidence, or is the model hallucinating? Relevance — does the answer actually address what was asked? Soundness — is the reasoning logical and coherent? And Depth — is it a real answer, or just a surface-level response?*

*Second: Quantitative evaluation. These are the classic information retrieval metrics. Precision measures how many of the retrieved chunks were actually relevant. Recall measures how many of the relevant chunks we actually found. And MRR — Mean Reciprocal Rank — tells you how high in the list the first correct result appeared. These are measured against human-labeled relevance judgments.*

*Third: Performance. Because a system that gives great answers but takes 45 seconds per query, or costs ten dollars per call, is not going to make it to production. So we measure latency — how long it takes — and token cost — how much we're spending on LLM calls. And we compare all of this against the baseline simple RAG, so we can actually quantify the improvement."*

---

### 📌 Glossary — Slide 11

| Term | What it means |
|---|---|
| **LLM-as-Judge** | Using a language model to evaluate the quality of another model's output — assessing faithfulness, relevance, etc. |
| **Faithfulness** | Whether the generated answer is supported by the retrieved evidence — not made up. The opposite of hallucination. |
| **Hallucination** | When an LLM generates information that sounds plausible but is factually incorrect or not grounded in any source. |
| **Relevance** | Whether the answer actually addresses the user's question. |
| **Soundness** | Whether the reasoning in the answer is logically valid and internally consistent. |
| **Precision (retrieval)** | Of all the chunks retrieved, what fraction were actually relevant? High precision = fewer irrelevant results. |
| **Recall (retrieval)** | Of all the relevant chunks in the index, what fraction did we actually retrieve? High recall = fewer relevant results missed. |
| **MRR (Mean Reciprocal Rank)** | A metric that measures how early the first correct result appears in the ranked list. If the correct answer is always first, MRR = 1.0. |
| **Latency** | The time it takes for the system to produce an answer after receiving the query. Usually measured in seconds. |
| **Token cost** | The cost of running the pipeline, measured in LLM tokens consumed. Lower is cheaper. |
| **Baseline** | The comparison point — in this case, the simple RAG system — against which improvements are measured. |

---

---

## Slide 12 — Basic RAG vs. Agentic RAG Comparison

*"Let's put it all side by side. This table makes the architectural differences very concrete.*

*Query Decomposition — basic RAG sends a single query. Agentic RAG builds a structured multi-step plan.*

*Retrieval Strategy — basic RAG uses one fixed method. Ours dynamically picks the best strategy per sub-query.*

*Query Optimization — basic RAG sends the raw user question directly to the retriever. We rewrite it first into something precise and keyword-rich.*

*Result Precision — basic RAG returns the top-K most similar chunks. We go further with CrossEncoder reranking to get genuinely high-precision top-3.*

*Context Compression — basic RAG dumps full chunks into the LLM. We distill them first so the model only sees the signal.*

*Memory and State — basic RAG is stateless. Every query is processed in isolation. Our system maintains full persistent state across all steps.*

*Revision Ability — basic RAG has one shot. Our policy agent can detect dead ends and revise the plan mid-execution.*

*External Knowledge — basic RAG is limited to whatever documents you've indexed. We can pull from the live web.*

*And the result? Basic RAG often gives incomplete answers on multi-hop questions. Ours delivers comprehensive, cited, multi-source answers.*

*Every one of these differences traces back to the same root cause — basic RAG has a simple architecture. Agentic RAG has a thinking architecture."*

---

### 📌 Glossary — Slide 12

| Term | What it means |
|---|---|
| **Multi-hop query** | A question that requires chaining multiple pieces of information to reach an answer — basic RAG can't do this reliably. |
| **Stateless vs. Stateful** | Stateless = no memory between steps. Stateful = persistent memory across all steps. This is one of the most fundamental differences between the two approaches. |
| **Dynamic strategy selection** | The ability to choose a different approach based on what's being asked, rather than always using the same method. |

---

---

## Slide 13 — Key Takeaways

*"Alright, let's bring it home. Six takeaways I want you to walk out with.*

*First: Architecture beats intelligence. When a RAG system fails, the instinct is to upgrade the LLM. But usually the LLM is not the problem. The architecture is. Agentic loops are the fix.*

*Second: Think in cycles, not lines. Human researchers don't search once and write a report. They gather information, realize they need more, go back, refine their understanding, and then synthesize. Our pipeline mirrors that process. Plan, retrieve, refine, reflect, critique, synthesize — and repeat if needed.*

*Third: Metadata is a superpower. Tagging your chunks with section-level context is a small extra step in pre-processing that pays enormous dividends in retrieval precision. Don't skip it.*

*Fourth: State is everything. Without RAGState, every step is blind to what came before. With it, the agent accumulates understanding. That's what transforms a sequence of lookups into something that resembles actual reasoning.*

*Fifth: Use a dual LLM strategy. You don't need your most expensive model for every task. Use the powerful model where it counts — planning and synthesis. Use the fast, cheap model for the routine stuff. You'll cut costs without sacrificing quality where it matters.*

*And sixth: Measure all three dimensions. Qualitative quality. Retrieval metrics. Performance. All three. If you only measure one, you'll optimize for the wrong thing.*

*Thank you — I'm happy to take questions."*

---

### 📌 Glossary — Slide 13

| Term | What it means |
|---|---|
| **Agentic loop** | The core repeating cycle of the pipeline: act → observe → decide → (continue / revise / finish). |
| **Synthesis** | The final step — combining all gathered evidence into one coherent, comprehensive answer. |
| **Production-grade** | A system robust and efficient enough to be deployed in a real business environment, not just a proof-of-concept demo. |
| **Cumulative understanding** | The progressive building of knowledge as each step adds to what the agent already knows, rather than starting fresh each time. |

---

*End of speech script. Total estimated speaking time: approximately 25–35 minutes depending on pace and Q&A.*

---
---

## Supplemental Q&A — The Planning Agent: Code Deep Dive

> **When to use this section:** This is bonus material for when an audience member asks "but what does the code actually look like?" or "does it really execute and produce output?" Deliver it conversationally, not as a lecture — you're walking them through real code they can go look at themselves.

---

*"Great question — let me pull back the curtain on what the planning agent actually is as code, because I think seeing the real artifacts makes everything click.*

*There are four distinct pieces of code that together make up what we call the Planning Agent. Let me walk through each one.*

---

*The first artifact is the **Step class**. This is a Pydantic data class — think of it as a typed contract that defines exactly what a single research step looks like. It has five fields: the sub-question to answer, a justification for why this step exists, the tool to use, a list of keywords, and optionally a document section like 'Item 1A. Risk Factors'.*

*The most important field is the tool field. It's declared as a Literal type — meaning the only two legal values are 'search_10k' or 'search_web'. That's it. The LLM cannot write 'I'll use the database' or 'let me check online' — it must pick one of those two exact strings. That strictness is what makes the downstream code reliable.*

---

*The second artifact is the **Plan class**. Even simpler — it's just a container that holds a list of Step objects. That's the complete research strategy: an ordered list of steps, each fully typed and validated.*

---

*The third artifact is the **RAGState dictionary**. This is the shared memory of the entire pipeline — a TypedDict that every single agent reads from and writes back to. It holds the original question, the plan, the accumulated research history in past_steps, which step we're currently on, the raw and reranked documents from the current retrieval, the distilled context, and the final answer slot. Every node in the LangGraph workflow receives this dictionary as input and returns an updated version of it as output. It's the thread that connects all the phases together.*

---

*The fourth and final artifact is the **planner agent itself** — and this is where actual execution happens.*

*Here's what the code looks like:*

*First, you define a system prompt using LangChain's ChatPromptTemplate. The system message gives the LLM its persona — 'You are an expert research planner' — and tells it explicitly what tools exist and when to use each one. The human message is just a template slot for the user's actual question.*

*Then you initialize the reasoning LLM — the most powerful model in your config — with temperature set to zero. Temperature zero means deterministic output. Planning is not creative work. You want the same question to produce the same plan every time.*

*Then you wire them together with LangChain's pipe operator: prompt, then the LLM, then .with_structured_output(Plan). That last part is the key — it tells LangChain to use the model's function-calling capability under the hood to force the output into the exact shape of our Plan Pydantic class. If the model produces something that doesn't match the schema, it errors out. No silent failures.*

*Finally, you call .invoke() with the question. That makes a real API call to OpenAI. And what comes back is not a string — it's an actual Python Plan object with real typed fields you can iterate over in code.*

---

*So what does the output actually look like? Let's say the query is something like: 'What are NVIDIA's competitive risks in the semiconductor space, and what is AMD doing in AI chips in 2024?'*

*The agent produces a Plan with two steps:*

*Step one: sub_question is 'What are the key risks related to competition as stated in the 10-K?' — tool is search_10k — keywords are competition, risk factors, semiconductor industry — document_section is 'Item 1A. Risk Factors'. Notice it not only picked the right tool, it figured out exactly which section of a 200-page document to search.*

*Step two: sub_question is 'What are the recent news and developments in AMD's AI chip strategy?' — tool is search_web — keywords are AMD, AI chip strategy, 2024, MI300X — document_section is None, because this is a web search, there's no document section.*

*That's real output from a real execution. The agent correctly identified that the first question lives in a 2023 filing, and the second question requires live internet data because 2024 news can't possibly be in a 2023 document.*

---

*So to directly answer the question — yes, it executes. Yes, it produces real output. The artifact is a typed Python object, not prose. And that structure is what makes the entire rest of the pipeline trustworthy — every downstream agent can rely on plan.steps[i].tool being exactly 'search_10k' or 'search_web', never anything else.*

*The whole thing is built on three technologies working together: Pydantic for strict schema enforcement, LangChain for wiring prompt to model to structured output parser, and LangGraph for the stateful multi-agent workflow that passes RAGState between nodes. You don't need to understand the internals of all three to use this pattern — but knowing they exist tells you this is production-ready infrastructure, not a prompt hack."*

---

### 📌 Glossary — Planning Agent Deep Dive

| Term | What it means |
|---|---|
| **Pydantic** | A Python library for data validation using type annotations. When you define a class with `BaseModel`, Pydantic enforces that every field has the correct type at runtime — not just in the IDE. |
| **BaseModel** | The Pydantic base class. Any class that inherits from it gets automatic validation, serialization, and schema generation. |
| **TypedDict** | A Python type hint that describes a dictionary with specific keys and value types. Used for RAGState — it's a plain dict at runtime but gives type-safety in the editor. |
| **Literal type** | A type annotation that restricts a field to a fixed set of exact values. `Literal["search_10k", "search_web"]` means only those two strings are valid — anything else raises a validation error. |
| **LangChain** | A Python framework for building applications with LLMs. Provides building blocks like prompt templates, LLM wrappers, output parsers, and the pipe (`\|`) operator for chaining them. |
| **ChatPromptTemplate** | A LangChain class that defines a reusable prompt with a system message (persona/rules) and a human message (the actual input). Variable slots like `{question}` are filled in at invoke time. |
| **Pipe operator (`\|`)** | In LangChain, `A \| B \| C` means: run A, pass its output to B, pass that output to C. It chains prompt → LLM → output parser into one callable object. |
| **`.with_structured_output(Plan)`** | A LangChain method that uses the model's function-calling/tool-use capability to force the LLM's output to match a specific Pydantic schema. More reliable than prompting for JSON and then parsing it manually. |
| **`.invoke()`** | The method that actually executes the chain — makes the real API call and returns the result. |
| **temperature=0** | An LLM parameter controlling randomness. 0 = fully deterministic (same input always gives same output). Used here because planning should be consistent and predictable, not creative. |
| **Function calling** | An OpenAI API feature where the model is given a schema and instructed to return JSON that matches it exactly, rather than free-form text. LangChain's `.with_structured_output()` uses this under the hood. |
| **LangGraph** | A LangChain extension for building stateful, multi-agent workflows as graphs. Each node is a function that reads RAGState and returns an updated RAGState. The edges between nodes define the control flow. |
| **Node** | In LangGraph, a single processing step in the workflow — e.g., the planner node, the retrieval node, the reflection node. Each node is a Python function. |
| **Structured output** | Output from an LLM that conforms to a defined schema — a typed Python object, not a free-form string. Much more reliable for downstream code to consume. |
| **ChatOpenAI** | LangChain's wrapper around the OpenAI chat API. Takes model name, temperature, and other settings. Swappable for other providers like Anthropic or local models like Ollama. |

---

