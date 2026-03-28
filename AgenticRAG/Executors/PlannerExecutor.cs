using System.Text.Json.Serialization;
using AgenticRAG.Models;
using AgenticRAG.Services;
using Microsoft.Agents.AI.Workflows;

namespace AgenticRAG.Executors;

/// <summary>
/// Node 1 — Planner
///
/// Receives the user's complex query, uses the reasoning LLM to decompose it into
/// a structured multi-step research plan, writes the plan to shared workflow state,
/// and fires the first StepSignal to start the research loop.
///
/// Each plan step declares:
///   - subQuestion : the specific question to answer
///   - reasoning   : why this step is needed
///   - tool        : "search_10k" (internal) or "search_web" (Tavily)
/// </summary>
[SendsMessage(typeof(StepSignal))]
public sealed class PlannerExecutor : Executor<UserQuery>
{
    private readonly AzureAIService _ai;

    internal const string StateScope = "rag";
    internal const string StateKey   = "state";

    public PlannerExecutor(AzureAIService ai) : base("Planner") => _ai = ai;

    public override async ValueTask HandleAsync(
        UserQuery message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine($"[Planner] Decomposing query into a research plan...");
        Console.WriteLine($"  Query: {message.Query}");

        const string SystemPrompt = """
            You are a strategic research planning agent.

            Task: Decompose the user's complex query into a minimal set of focused sub-questions.
            For each sub-question choose the correct tool:
              • "search_10k"  — facts from NVIDIA's internal 2023/2024 10-K SEC filing
                                (business model, risks, financials, competitive landscape)
              • "search_web"  — current events, news, or information beyond the filing date

            Rules:
            - Include only steps that are strictly necessary to answer the query.
            - Prefer 2-4 steps; avoid redundant steps.
            - Sequence steps so earlier findings can inform later queries.

            Return JSON exactly:
            {
              "steps": [
                { "subQuestion": "...", "reasoning": "...", "tool": "search_10k" },
                { "subQuestion": "...", "reasoning": "...", "tool": "search_web"  }
              ]
            }
            """;

        var plan = await _ai.CompleteStructuredAsync<ResearchPlan>(
            SystemPrompt,
            $"Create a research plan for:\n\n{message.Query}",
            useReasoningModel: true,
            cancellationToken);

        // Ensure there is at least one step even if the LLM returns nothing
        var steps = plan?.Steps ?? new List<ResearchStep>();
        if (steps.Count == 0)
        {
            steps.Add(new ResearchStep
            {
                SubQuestion = message.Query,
                Reasoning   = "Direct fallback — no plan was generated.",
                Tool        = "search_10k"
            });
        }

        Console.WriteLine($"[Planner] {steps.Count}-step plan created:");
        for (int i = 0; i < steps.Count; i++)
            Console.WriteLine($"  {i + 1}. [{steps[i].Tool,-12}] {steps[i].SubQuestion}");

        // Initialise and persist the shared RAG state
        var state = new RagState
        {
            UserQuery      = message.Query,
            Plan           = steps,
            CurrentStepIndex = 0,
            IterationCount = 0
        };
        await context.QueueStateUpdateAsync(StateKey, state, scopeName: StateScope, cancellationToken);

        // Kick off the research loop at step 0
        await context.SendMessageAsync(new StepSignal(0), cancellationToken: cancellationToken);
    }
}
