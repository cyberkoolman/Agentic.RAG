// Copyright (c) Microsoft. All rights reserved.
// Agentic RAG — AG-UI Server
// Exposes the multi-hop RAG pipeline over the AG-UI protocol (SSE)
// and the DevUI chat window for interactive testing.

using AgenticRAG.Api;
using AgenticRAG.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load configuration (same pattern as the console app)
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true,  reloadOnChange: false);

builder.Services.AddHttpClient().AddLogging();
builder.Services.AddAGUI();
builder.Services.AddDevUI();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ─── Register the RAG agent in DI so DevUI can discover it ───────────────────

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.");

var ragClient = new RagWorkflowChatClient(settings);

builder.Services.AddAIAgent(
    name:         "AgenticRAGAssistant",
    instructions: "You are a deep-thinking research assistant powered by an Agentic RAG pipeline.",
    chatClient:   ragClient);

// ─── Build and configure the pipeline ────────────────────────────────────────

WebApplication app = builder.Build();

app.UseCors();

// Resolve the registered agent for MapAGUI
var agent = app.Services.GetRequiredService<AIAgent>();

app.MapAGUI("/api/run", agent);
app.MapDevUI();              // Chat window at /devui

Console.WriteLine(new string('═', 60));
Console.WriteLine("  AG-UI Server — Agentic Deep-Thinking RAG Pipeline");
Console.WriteLine("  Chat UI  →  http://localhost:8888/devui");
Console.WriteLine("  AG-UI    →  POST http://localhost:8888/api/run");
Console.WriteLine(new string('═', 60));

await app.RunAsync();
