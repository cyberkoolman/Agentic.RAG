using HtmlAgilityPack;
using AgenticRAG.Models;
using AgenticRAG.Configuration;
using System.Net;
using System.Text;

namespace AgenticRAG.Services;

/// <summary>
/// Downloads the NVIDIA 2024 10-K filing from SEC EDGAR, parses the HTML,
/// splits the text into overlapping chunks with section metadata,
/// and generates embeddings via AzureAIService.
/// </summary>
public class DocumentLoader
{
    private readonly PipelineSettings _pipeline;
    private readonly string _filingUrl;

    // Approximate characters per token (GPT tokeniser averages ~4 chars/token)
    private const int CharsPerToken = 4;

    public DocumentLoader(AppSettings settings)
    {
        _pipeline   = settings.Pipeline;
        _filingUrl  = settings.Knowledge.NvidiaFilingUrl;
    }

    // ─────────────────────────────────────────────────────────────
    // Public entry point
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads, parses, chunks and embeds the NVIDIA 10-K filing.
    /// Returns a list of RagDocuments ready for insertion into VectorStore.
    /// </summary>
    public async Task<List<RagDocument>> LoadNvidiaFilingAsync(
        AzureAIService aiService,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"  Downloading NVIDIA 10-K from SEC EDGAR...");
        var html = await DownloadAsync(_filingUrl, cancellationToken);

        Console.WriteLine("  Parsing and chunking document...");
        var chunks = ParseAndChunk(html);
        Console.WriteLine($"  Created {chunks.Count} chunks.");

        Console.WriteLine($"  Generating embeddings (batched)...");
        var documents = await EmbedChunksAsync(chunks, aiService, cancellationToken);

        return documents;
    }

    // ─────────────────────────────────────────────────────────────
    // Download
    // ─────────────────────────────────────────────────────────────

    private static async Task<string> DownloadAsync(string url, CancellationToken ct)
    {
        using var http = new HttpClient();
        // SEC EDGAR requires a descriptive User-Agent.
        // TryAddWithoutValidation bypasses strict RFC token validation
        // that would reject the '@' in an email address.
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "AgenticRAG Research Bot research@example.com");
        return await http.GetStringAsync(url, ct);
    }

    // ─────────────────────────────────────────────────────────────
    // HTML → chunks
    // ─────────────────────────────────────────────────────────────

    private List<(string Content, string Section)> ParseAndChunk(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Strip non-content nodes
        RemoveNodes(doc, "//script", "//style", "//head", "//nav", "//footer");

        var chunks        = new List<(string Content, string Section)>();
        var currentSection = "General";
        var buffer        = new StringBuilder();
        int chunkCharSize  = _pipeline.ChunkSize   * CharsPerToken;
        int overlapCharSize = _pipeline.ChunkOverlap * CharsPerToken;

        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            if (node.NodeType != HtmlNodeType.Text) continue;

            var text = WebUtility.HtmlDecode(node.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Detect SEC "Item X." section headers  (e.g. "Item 1A. Risk Factors")
            if (IsItemHeader(text))
                currentSection = text.Length > 80 ? text[..80] : text;

            buffer.Append(' ').Append(text);

            // Flush to chunks once buffer is large enough
            if (buffer.Length >= chunkCharSize * 2)
            {
                FlushBuffer(buffer, currentSection, chunkCharSize, overlapCharSize, chunks);
            }
        }

        // Flush remainder
        if (buffer.Length > 100)
            FlushBuffer(buffer, currentSection, chunkCharSize, overlapCharSize, chunks);

        return chunks;
    }

    private static void FlushBuffer(
        StringBuilder buffer,
        string section,
        int chunkSize,
        int overlap,
        List<(string, string)> chunks)
    {
        var text = buffer.ToString().Trim();
        int start = 0;

        while (start < text.Length)
        {
            int end     = Math.Min(start + chunkSize, text.Length);
            var chunk   = text[start..end].Trim();
            if (chunk.Length > 80)
                chunks.Add((chunk, section));
            start += chunkSize - overlap;
        }

        // Keep only overlap tail in the buffer for next flush
        if (text.Length > overlap)
        {
            var tail = text[^overlap..];
            buffer.Clear().Append(tail);
        }
        else
        {
            buffer.Clear();
        }
    }

    private static bool IsItemHeader(string text) =>
        text.StartsWith("Item ", StringComparison.OrdinalIgnoreCase) &&
        text.Length < 120 &&
        char.IsDigit(text.ElementAtOrDefault(5));

    private static void RemoveNodes(HtmlDocument doc, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes is null) continue;
            foreach (var n in nodes.ToList()) n.Remove();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Embed in batches
    // ─────────────────────────────────────────────────────────────

    private async Task<List<RagDocument>> EmbedChunksAsync(
        List<(string Content, string Section)> chunks,
        AzureAIService aiService,
        CancellationToken cancellationToken)
    {
        const int BatchSize = 16; // Keeps each API call comfortably within limits

        var documents = new List<RagDocument>(chunks.Count);

        for (int i = 0; i < chunks.Count; i += BatchSize)
        {
            var batch  = chunks.Skip(i).Take(BatchSize).ToList();
            var texts  = batch.Select(c => c.Content).ToList();
            var embeds = await aiService.GenerateEmbeddingsAsync(texts, cancellationToken);

            for (int j = 0; j < batch.Count; j++)
            {
                documents.Add(new RagDocument
                {
                    Content       = batch[j].Content,
                    Section       = batch[j].Section,
                    Source        = "NVIDIA 2024 10-K",
                    Url           = _filingUrl,
                    Embedding     = embeds[j]
                });
            }

            int done = Math.Min(i + BatchSize, chunks.Count);
            Console.WriteLine($"    Embedded {done}/{chunks.Count} chunks...");
        }

        return documents;
    }
}
