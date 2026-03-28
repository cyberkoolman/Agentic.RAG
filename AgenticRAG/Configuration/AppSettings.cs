namespace AgenticRAG.Configuration;

public class AppSettings
{
    public AzureAISettings AzureAI { get; set; } = new();
    public TavilySettings Tavily { get; set; } = new();
    public PipelineSettings Pipeline { get; set; } = new();
    public KnowledgeSettings Knowledge { get; set; } = new();
}

public class AzureAISettings
{
    /// <summary>Azure AI Foundry or Azure OpenAI endpoint URL.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key for the endpoint. Leave empty to use DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Deployment name for the high-capability model (planning, policy, synthesis).</summary>
    public string ReasoningModel { get; set; } = "gpt-4o";

    /// <summary>Deployment name for the fast model (rewriting, reranking, distillation, reflection).</summary>
    public string FastModel { get; set; } = "gpt-4o-mini";

    /// <summary>Deployment name for the text embedding model.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public class TavilySettings
{
    /// <summary>Tavily Search API key.</summary>
    public string ApiKey { get; set; } = "";
}

public class PipelineSettings
{
    /// <summary>Approximate token size for each document chunk.</summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>Token overlap between consecutive chunks.</summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>Number of documents to retrieve before reranking.</summary>
    public int InitialRetrievalTopK { get; set; } = 10;

    /// <summary>Number of documents to keep after reranking.</summary>
    public int RerankerTopK { get; set; } = 3;

    /// <summary>Safety cap on research iterations to prevent infinite loops.</summary>
    public int MaxIterations { get; set; } = 10;
}

public class KnowledgeSettings
{
    /// <summary>URL of the NVIDIA 2024 10-K filing on SEC EDGAR.</summary>
    public string NvidiaFilingUrl { get; set; } =
        "https://www.sec.gov/Archives/edgar/data/1045810/000104581024000029/nvda-20240128.htm";
}
