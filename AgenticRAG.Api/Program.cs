// Copyright (c) Microsoft. All rights reserved.
// Agentic RAG — DevUI Chat Window

using AgenticRAG.Api;
using AgenticRAG.Configuration;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
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

// ─── Register the RAG pipeline ────────────────────────────────────────────────

var settings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.");

builder.Services.AddSingleton<IChatClient>(new RagWorkflowChatClient(settings));

builder.AddAIAgent(
    name:         "AgenticRAGAssistant",
    instructions: "You are a deep-thinking research assistant powered by an Agentic RAG pipeline.");

// ─── DevUI services ───────────────────────────────────────────────────────────

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.Services.AddDevUI();

// ─── Build and map ────────────────────────────────────────────────────────────

WebApplication app = builder.Build();

app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

Console.WriteLine(new string('═', 60));
Console.WriteLine("  Agentic RAG — DevUI Chat");
Console.WriteLine("  http://localhost:8888/devui");
Console.WriteLine(new string('═', 60));

await app.RunAsync();
