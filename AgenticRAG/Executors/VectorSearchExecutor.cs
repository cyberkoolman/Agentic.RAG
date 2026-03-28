using AgenticRAG.Models;
using AgenticRAG.Services;
using Microsoft.Agents.AI.Workflows;

namespace AgenticRAG.Executors;

/// <summary>
/// Node 3a — Vector / Keyword / Hybrid Search (internal knowledge base)
///
/// Handles SearchRequests routed here when tool == "search_10k".
/// Internally contains a Retrieval Supervisor that uses the fast LLM to select the
/// best search strategy (vector, keyword, or hybrid) for the current query.
///
/// Retrieval funnel — Stage 1: Broad recall
///   Executes the selected strategy against the in-memory VectorStore and returns
///   InitialRetrievalTopK candidates to the Reranker.
/// </summary>
[SendsMessage(typeof(SearchResults))]
public sealed class VectorSearchExecutor : Executor<SearchRequest>
{
    private readonly AzureAIService _ai;
    private readonly VectorStore    _store;
    private readonly int            _topK;

    public VectorSearchExecutor(AzureAIService ai, VectorStore store, int topK = 10)
        : base("VectorSearch")
    {
        _ai    = ai;
        _store = store;
        _topK  = topK;
    }

    public override async ValueTask HandleAsync(
        SearchRequest message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[VectorSearch] Searching 10-K index...");
        Console.WriteLine($"  Query : {message.RewrittenQuery}");

        // ── Retrieval Supervisor: choose the best strategy ──────────────────
        var strategy = await ChooseStrategyAsync(message.RewrittenQuery, cancellationToken);
        Console.WriteLine($"  Strategy selected: {strategy}");

        // ── Execute search ───────────────────────────────────────────────────
        List<RagDocument> results;

        if (strategy == SearchStrategy.Vector || strategy == SearchStrategy.Hybrid)
        {
            // Embed the query only when needed (vector or hybrid)
            var embedding = await _ai.GenerateEmbeddingAsync(message.RewrittenQuery, cancellationToken);
            results = strategy == SearchStrategy.Hybrid
                ? _store.HybridSearch(message.RewrittenQuery, embedding, _topK)
                : _store.VectorSearch(embedding, _topK);
        }
        else
        {
            results = _store.KeywordSearch(message.RewrittenQuery, _topK);
        }

        Console.WriteLine($"  Retrieved {results.Count} candidate documents.");

        await context.SendMessageAsync(
            new SearchResults(results, message.OriginalSubQuestion, message.StepIndex),
            cancellationToken: cancellationToken);
    }

    // ── Retrieval Supervisor ─────────────────────────────────────────────────

    private async Task<SearchStrategy> ChooseStrategyAsync(string query, CancellationToken ct)
    {
        const string SystemPrompt = """
            You are a retrieval strategy supervisor for a document search engine.

            Choose the single best search strategy for the query:
              • vector   — conceptual, thematic, or paraphrased questions
              • keyword  — queries containing exact names, acronyms, numbers, or quoted phrases
              • hybrid   — queries that benefit from both precision and semantic recall

            Respond with exactly one word: vector, keyword, or hybrid.
            """;

        var response = await _ai.CompleteAsync(SystemPrompt, $"Query: {query}", useReasoningModel: false, ct);

        return response.Trim().ToLowerInvariant() switch
        {
            "keyword" => SearchStrategy.Keyword,
            "vector"  => SearchStrategy.Vector,
            _         => SearchStrategy.Hybrid
        };
    }
}
