using AgenticRAG.Configuration;
using AgenticRAG.Models;
using AgenticRAG.Services;
using AgenticRAG.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI.Workflows;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true,  reloadOnChange: false) // git-ignored; overrides keys
    .Build();

var settings = config.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.json.");

ValidateSettings(settings);

Console.WriteLine(new string('═', 80));
Console.WriteLine("  Agentic Deep-Thinking RAG Pipeline");
Console.WriteLine("  Microsoft Agent Framework  |  Azure AI Foundry  |  Tavily Search");
Console.WriteLine(new string('═', 80));
Console.WriteLine($"  Reasoning model : {settings.AzureAI.ReasoningModel}");
Console.WriteLine($"  Fast model      : {settings.AzureAI.FastModel}");
Console.WriteLine($"  Embedding model : {settings.AzureAI.EmbeddingModel}");
Console.WriteLine($"  Chunk size      : {settings.Pipeline.ChunkSize} tokens");
Console.WriteLine($"  Retrieval top-K : {settings.Pipeline.InitialRetrievalTopK}  →  Reranker top-K: {settings.Pipeline.RerankerTopK}");
Console.WriteLine(new string('─', 80));

// ─────────────────────────────────────────────────────────────────────────────
// Initialise services
// ─────────────────────────────────────────────────────────────────────────────

var aiService      = new AzureAIService(settings);
var vectorStore    = new VectorStore();
var tavilyService  = new TavilyService(settings);
var documentLoader = new DocumentLoader(settings);

// ─────────────────────────────────────────────────────────────────────────────
// Build the knowledge base (download → parse → embed → index)
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n[Init] Building knowledge base from NVIDIA 2024 10-K...");
var documents = await documentLoader.LoadNvidiaFilingAsync(aiService);
vectorStore.AddDocuments(documents);
Console.WriteLine($"[Init] Knowledge base ready — {vectorStore.DocumentCount} chunks indexed.");

// ─────────────────────────────────────────────────────────────────────────────
// Build the workflow graph
// ─────────────────────────────────────────────────────────────────────────────

var workflow = AgenticRagWorkflow.Build(aiService, vectorStore, tavilyService, settings.Pipeline);

// ─────────────────────────────────────────────────────────────────────────────
// Define the test query
//
// This is the canonical multi-source, multi-hop query from the article:
// - Hop 1: NVIDIA's competitive risks from AMD  → search_10k
// - Hop 2: AMD's post-2023 product strategy     → search_web
// - Synthesis: How does hop-2 exacerbate hop-1?
// ─────────────────────────────────────────────────────────────────────────────

var query = args.Length > 0
    ? string.Join(" ", args)   // Allow passing a custom query via CLI args
    : "What were NVIDIA's main competitive risks from AMD disclosed in their 2023 10-K filing, " +
      "and how does AMD's recent product strategy (post-2023) exacerbate those specific risks?";

Console.WriteLine($"\n[Query] {query}");

// ─────────────────────────────────────────────────────────────────────────────
// Run the workflow
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("\n[Pipeline] Starting Deep-Thinking RAG loop...\n");

var startTime = DateTimeOffset.UtcNow;

await using var run = await InProcessExecution.RunStreamingAsync(
    workflow,
    new UserQuery(query));

string? finalAnswer = null;

await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        finalAnswer = outputEvent.Data as string;
        break;
    }
}

var elapsed = DateTimeOffset.UtcNow - startTime;

// ─────────────────────────────────────────────────────────────────────────────
// Summary
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine($"\n[Done] Workflow completed in {elapsed.TotalSeconds:F1}s");

if (finalAnswer is null)
    Console.WriteLine("[Warning] No output was yielded by the workflow.");

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static void ValidateSettings(AppSettings s)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(s.AzureAI.Endpoint))
        errors.Add("AzureAI:Endpoint is not set in appsettings.json");

    if (string.IsNullOrWhiteSpace(s.AzureAI.ApiKey))
        errors.Add("AzureAI:ApiKey is not set — set it or configure DefaultAzureCredential");

    if (string.IsNullOrWhiteSpace(s.Tavily.ApiKey))
        errors.Add("Tavily:ApiKey is not set in appsettings.json");

    if (errors.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[Config] Missing configuration values:");
        foreach (var e in errors) Console.WriteLine($"  • {e}");
        Console.ResetColor();
        Console.WriteLine();
        // Don't abort — let the user see all missing fields and proceed if they want.
    }
}
