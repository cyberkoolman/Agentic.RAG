using System.Text.Json.Serialization;
using AgenticRAG.Models;
using AgenticRAG.Services;
using Microsoft.Agents.AI.Workflows;

namespace AgenticRAG.Executors;

/// <summary>
/// Node 1 — Planner
///
/// Decomposes the user's query into a minimal, ordered research plan.
/// Each step declares which tool to use:
///   • search_docs — search the indexed internal knowledge base
///   • search_web  — live web search via Tavily (for current events or gaps in the KB)
///
/// The knowledge base description is injected at construction time from appsettings,
/// making the planner aware of what documents are available without hardcoding.
/// </summary>
[SendsMessage(typeof(StepSignal))]
public sealed class PlannerExecutor : Executor<UserQuery>
{
    private readonly AzureAIService _ai;
    private readonly string         _kbDescription;

    internal const string StateScope = "rag";
    internal const string StateKey   = "state";

    /// <param name="kbDescription">
    /// Human-readable list of indexed sources, e.g.
    /// "NVIDIA 2024 10-K (SEC annual filing), AMD 2024 10-K (SEC annual filing)"
    /// Built from DocumentSource.Name + Description in Program.cs.
    /// </param>
    public PlannerExecutor(AzureAIService ai, string kbDescription) : base("Planner")
    {
        _ai            = ai;
        _kbDescription = kbDescription;
    }

    public override async ValueTask HandleAsync(
        UserQuery message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n" + new string('─', 80));
        Console.WriteLine("[Planner] Decomposing query into a research plan...");
        Console.WriteLine($"  Query: {message.Query}");

        var systemPrompt = $$"""
            You are a strategic research planning agent.

            Available tools:
              • search_docs — searches the internal knowledge base which contains:
                {{_kbDescription}}
              • search_web  — performs a live web search (use for current events,
                recent news, or anything not covered by the internal documents)

            Task: Decompose the user's query into a minimal, ordered set of sub-questions.
            For each step choose the correct tool based on where the answer is likely to be found.

            Rules:
            - Use search_docs first when the answer should exist in the knowledge base.
            - Use search_web when the answer requires live or post-publication information.
            - Keep the plan to 2–5 steps; avoid redundant steps.
            - Sequence steps so earlier findings can enrich later queries.

            Return JSON exactly:
            {
              "steps": [
                { "subQuestion": "...", "reasoning": "...", "tool": "search_docs" },
                { "subQuestion": "...", "reasoning": "...", "tool": "search_web"  }
              ]
            }
            """;

        var plan = await _ai.CompleteStructuredAsync<ResearchPlan>(
            systemPrompt,
            $"Create a research plan for:\n\n{message.Query}",
            useReasoningModel: true,
            cancellationToken);

        var steps = plan?.Steps ?? new List<ResearchStep>();
        if (steps.Count == 0)
        {
            steps.Add(new ResearchStep
            {
                SubQuestion = message.Query,
                Reasoning   = "Direct fallback — no plan was generated.",
                Tool        = "search_docs"
            });
        }

        Console.WriteLine($"[Planner] {steps.Count}-step plan created:");
        for (int i = 0; i < steps.Count; i++)
            Console.WriteLine($"  {i + 1}. [{steps[i].Tool,-12}] {steps[i].SubQuestion}");

        var state = new RagState
        {
            UserQuery        = message.Query,
            Plan             = steps,
            CurrentStepIndex = 0,
            IterationCount   = 0
        };
        await context.QueueStateUpdateAsync(StateKey, state, scopeName: StateScope, cancellationToken);

        await context.SendMessageAsync(new StepSignal(0), cancellationToken: cancellationToken);
    }
}
