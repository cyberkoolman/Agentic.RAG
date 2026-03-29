using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AgenticRAG.Configuration;
using AgenticRAG.Models;
using AgenticRAG.Services;
using AgenticRAG.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgenticRAG.Api;

/// <summary>
/// Stateful IChatClient that wraps the Agentic RAG pipeline.
///
/// Behaviour:
///   • On startup          — idle, no documents loaded.
///   • User sends a URL    — fetches, chunks, embeds, and indexes it.
///   • User sends a query  — runs the multi-hop RAG pipeline.
///
/// Multiple sources can be added at any time; each one is appended to
/// the shared VectorStore and the Planner's KB description is updated.
/// </summary>
public sealed class RagWorkflowChatClient : IChatClient
{
    // ── Services (created once, cheap) ────────────────────────────────────
    private readonly AppSettings   _settings;
    private readonly AzureAIService   _ai;
    private readonly VectorStore      _store;
    private readonly TavilyService    _tavily;
    private readonly DocumentLoader   _loader;
    private readonly SemaphoreSlim    _lock = new(1, 1);

    // ── State ─────────────────────────────────────────────────────────────
    private bool   _hasSource;
    private string _kbDescription = "";
    private Microsoft.Agents.AI.Workflows.Workflow? _workflow;

    public RagWorkflowChatClient(AppSettings settings)
    {
        _settings = settings;
        _ai       = new AzureAIService(settings);
        _store    = new VectorStore();
        _tavily   = new TavilyService(settings);
        _loader   = new DocumentLoader(settings);
    }

    // ── IChatClient ────────────────────────────────────────────────────────

    public ChatClientMetadata Metadata =>
        new("AgenticRAG", providerUri: null, defaultModelId: "agentic-rag-pipeline");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<string>();
        await foreach (var update in GetStreamingResponseAsync(chatMessages, options, cancellationToken))
            if (update.Text is { } t) chunks.Add(t);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(chunks)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userText = chatMessages
            .LastOrDefault(m => m.Role == ChatRole.User)?.Text?.Trim() ?? "";

        // ── Does the message contain a URL? ───────────────────────────────
        var url = ExtractUrl(userText);
        if (url is not null)
        {
            await foreach (var u in LoadSourceAsync(url, ct))
                yield return u;
            yield break;
        }

        // ── No source loaded yet ──────────────────────────────────────────
        if (!_hasSource)
        {
            yield return Text(
                "I'm ready! Please share a URL (or paste a file path) to load your " +
                "knowledge source, and I'll index it. Then ask me anything.");
            yield break;
        }

        // ── Run the RAG pipeline ──────────────────────────────────────────
        await foreach (var u in RunPipelineAsync(userText, ct))
            yield return u;
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() => _lock.Dispose();

    // ── Source loading ─────────────────────────────────────────────────────

    private async IAsyncEnumerable<ChatResponseUpdate> LoadSourceAsync(
        string url,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            yield return Text($"Loading `{url}` — this may take a minute while I chunk and embed the content…\n");

            var source = new DocumentSource
            {
                Name        = UrlToName(url),
                Url         = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "",
                FilePath    = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "" : url,
                Type        = "html",
                Description = ""
            };

            var docs = await _loader.LoadAllAsync([source], _ai, ct);
            _store.AddDocuments(docs);

            // Update KB description and rebuild the workflow
            _kbDescription = string.IsNullOrEmpty(_kbDescription)
                ? source.Name
                : _kbDescription + ", " + source.Name;

            _workflow  = AgenticRagWorkflow.Build(_ai, _store, _tavily, _settings.Pipeline, _kbDescription);
            _hasSource = true;

            Console.WriteLine($"[RAG] Source added: {source.Name} — {docs.Count} chunks, {_store.DocumentCount} total.");

            yield return Text(
                $"Done! Indexed **{docs.Count} chunks** from `{source.Name}`. " +
                $"Knowledge base now has **{_store.DocumentCount} chunks** total.\n\n" +
                "What would you like to know?");
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Pipeline execution ─────────────────────────────────────────────────

    private async IAsyncEnumerable<ChatResponseUpdate> RunPipelineAsync(
        string query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Console.WriteLine($"\n[RAG] Query: {query}");

        string? answer = null;
        await using var run = await InProcessExecution.RunStreamingAsync(
            _workflow!, new UserQuery(query));

        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent outputEvent)
            {
                answer = outputEvent.Data as string;
                break;
            }
        }

        answer ??= "The research pipeline did not produce an answer.";

        // Stream sentence by sentence
        var sentences = answer.Split([". ", ".\n"], StringSplitOptions.None);
        for (int i = 0; i < sentences.Length; i++)
        {
            var chunk = i < sentences.Length - 1 ? sentences[i] + ". " : sentences[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;
            yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(chunk)] };
            await Task.Delay(25, ct);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ChatResponseUpdate Text(string text) =>
        new() { Role = ChatRole.Assistant, Contents = [new TextContent(text)] };

    private static string? ExtractUrl(string text)
    {
        // Match http/https URLs or local file paths
        var urlMatch = Regex.Match(text,
            @"https?://[^\s]+",
            RegexOptions.IgnoreCase);
        if (urlMatch.Success) return urlMatch.Value;

        // Local file path: absolute path that exists
        var pathMatch = Regex.Match(text,
            @"[A-Za-z]:\\[^\s]+|/[^\s]+",
            RegexOptions.IgnoreCase);
        if (pathMatch.Success && File.Exists(pathMatch.Value))
            return pathMatch.Value;

        return null;
    }

    private static string UrlToName(string url)
    {
        try
        {
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var uri  = new Uri(url);
                var host = uri.Host.Replace("www.", "");
                var path = uri.AbsolutePath.Trim('/').Split('/').LastOrDefault() ?? "";
                return string.IsNullOrEmpty(path) ? host : $"{host}/{path}";
            }
            return Path.GetFileName(url);
        }
        catch { return url; }
    }
}
