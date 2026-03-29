using System.Runtime.CompilerServices;
using AgenticRAG.Configuration;
using AgenticRAG.Models;
using AgenticRAG.Services;
using AgenticRAG.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgenticRAG.Api;

/// <summary>
/// Adapts the multi-hop AgenticRagWorkflow to the IChatClient interface
/// required by Microsoft.Agents.AI and MapAGUI.
///
/// Initialization (document loading + embedding) happens once on the first
/// request via a lazy async pattern so the server starts up instantly.
/// </summary>
public sealed class RagWorkflowChatClient : IChatClient
{
    private readonly AppSettings    _settings;
    private readonly SemaphoreSlim  _initLock = new(1, 1);

    private Microsoft.Agents.AI.Workflows.Workflow? _workflow;
    private bool _initialized;

    public RagWorkflowChatClient(AppSettings settings) => _settings = settings;

    // ── IChatClient (Microsoft.Extensions.AI 10.x API) ────────────────────

    public ChatClientMetadata Metadata =>
        new("AgenticRAG", providerUri: null, defaultModelId: "agentic-rag-pipeline");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var answer = await RunPipelineAsync(chatMessages, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, answer));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var answer = await RunPipelineAsync(chatMessages, cancellationToken);

        // Stream the answer sentence-by-sentence for a natural feel.
        // (The workflow produces the full synthesis text in one shot;
        //  chunking here gives the AG-UI client a progressive display.)
        var sentences = answer.Split(new[] { ". ", ".\n" }, StringSplitOptions.None);
        for (int i = 0; i < sentences.Length; i++)
        {
            var chunk = i < sentences.Length - 1 ? sentences[i] + ". " : sentences[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            yield return new ChatResponseUpdate
            {
                Role     = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Delay(30, cancellationToken);
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() => _initLock.Dispose();

    // ── Pipeline execution ─────────────────────────────────────────────────

    private async Task<string> RunPipelineAsync(
        IEnumerable<ChatMessage> chatMessages,
        CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        var query = chatMessages
            .LastOrDefault(m => m.Role == ChatRole.User)
            ?.Text
            ?? throw new InvalidOperationException("No user message in the request.");

        Console.WriteLine($"\n[AG-UI] Query received: {query}");

        await using var run = await InProcessExecution.RunStreamingAsync(
            _workflow!, new UserQuery(query));

        string? finalAnswer = null;
        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                finalAnswer = outputEvent.Data as string;
                break;
            }
        }

        return finalAnswer ?? "The research pipeline did not produce an answer.";
    }

    // ── Lazy initialization ────────────────────────────────────────────────

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var aiService      = new AzureAIService(_settings);
            var vectorStore    = new VectorStore();
            var tavilyService  = new TavilyService(_settings);
            var documentLoader = new DocumentLoader(_settings);

            var sources = _settings.Knowledge.Sources;

            var kbDescription = sources.Count > 0
                ? string.Join(", ", sources.Select(s =>
                    string.IsNullOrWhiteSpace(s.Description) ? s.Name : $"{s.Name} ({s.Description})"))
                : "(no internal documents configured)";

            Console.WriteLine("[AG-UI Init] Building knowledge base...");
            foreach (var s in sources)
                Console.WriteLine($"  • {s.Name}");

            var documents = await documentLoader.LoadAllAsync(sources, aiService, ct);
            vectorStore.AddDocuments(documents);
            Console.WriteLine($"[AG-UI Init] {vectorStore.DocumentCount} chunks indexed.");

            _workflow    = AgenticRagWorkflow.Build(
                aiService, vectorStore, tavilyService, _settings.Pipeline, kbDescription);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
