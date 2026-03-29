// Copyright (c) Microsoft. All rights reserved.
// Agentic RAG — AG-UI Server + DevUI Chat Window

using AgenticRAG.Api;
using AgenticRAG.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json",       optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true,  reloadOnChange: false);

builder.Services.AddHttpClient().AddLogging();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ─── Register the RAG pipeline as an IChatClient ──────────────────────────────

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.");

var ragClient = new RagWorkflowChatClient(settings);

// Register as IChatClient in DI so AddAIAgent can resolve it
builder.Services.AddSingleton<IChatClient>(ragClient);

// Register agent (resolves IChatClient from DI) — DevUI discovers this
builder.AddAIAgent(
    name:         "AgenticRAGAssistant",
    instructions: "You are a deep-thinking research assistant powered by an Agentic RAG pipeline.");

// ─── Required services for DevUI conversation and response endpoints ──────────

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddAGUI();
builder.Services.AddDevUI();

// ─── Build ────────────────────────────────────────────────────────────────────

WebApplication app = builder.Build();

app.UseCors();

// ─── Map endpoints ────────────────────────────────────────────────────────────

// AG-UI SSE endpoint (for console client / external frontends)
var agent = app.Services.GetRequiredKeyedService<AIAgent>("AgenticRAGAssistant");
app.MapAGUI("/api/run", agent);

// DevUI requires these three mappings
app.MapOpenAIResponses();       // POST /v1/responses
app.MapOpenAIConversations();   // POST|GET /v1/conversations/*
app.MapDevUI();                 // GET  /devui

Console.WriteLine(new string('═', 60));
Console.WriteLine("  AG-UI Server — Agentic Deep-Thinking RAG Pipeline");
Console.WriteLine("  DevUI    →  http://localhost:8888/devui");
Console.WriteLine("  AG-UI    →  POST http://localhost:8888/api/run");
Console.WriteLine(new string('═', 60));

await app.RunAsync();
