// Copyright (c) Microsoft. All rights reserved.
// Agentic RAG — AG-UI Console Client
// Connects to the AgenticRAG.Api server and chats via the AG-UI protocol.

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

string serverUrl = Environment.GetEnvironmentVariable("AGUI_SERVER_URL") ?? "http://localhost:8888/api/run";

Console.WriteLine(new string('═', 60));
Console.WriteLine("  Agentic RAG — AG-UI Chat Client");
Console.WriteLine($"  Server: {serverUrl}");
Console.WriteLine(new string('═', 60));

// ── Create the AG-UI client agent (from docs Step 2) ─────────────────────────

using HttpClient httpClient = new()
{
    Timeout = TimeSpan.FromMinutes(5)   // RAG pipeline can take a while
};

AGUIChatClient chatClient = new(httpClient, serverUrl);

AIAgent agent = chatClient.AsAIAgent(
    name: "agui-client",
    description: "AG-UI Client for Agentic RAG Pipeline");

AgentSession session = await agent.CreateSessionAsync();
List<ChatMessage> messages =
[
    new(ChatRole.System, "You are a deep-thinking research assistant.")
];

// ── Chat loop ─────────────────────────────────────────────────────────────────

try
{
    while (true)
    {
        Console.WriteLine();
        Console.Write("Query (:q or quit to exit): ");
        string? message = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Query cannot be empty.");
            continue;
        }

        if (message is ":q" or "quit")
            break;

        messages.Add(new ChatMessage(ChatRole.User, message));

        bool   isFirstUpdate = true;
        string? threadId     = null;

        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, session))
        {
            ChatResponseUpdate chatUpdate = update.AsChatResponseUpdate();

            if (isFirstUpdate)
            {
                threadId = chatUpdate.ConversationId;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Run Started — Thread: {chatUpdate.ConversationId}, Run: {chatUpdate.ResponseId}]");
                Console.ResetColor();
                Console.WriteLine();
                isFirstUpdate = false;
            }

            foreach (AIContent content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(textContent.Text);
                    Console.ResetColor();
                }
                else if (content is ErrorContent errorContent)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Error: {errorContent.Message}]");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[Run Finished — Thread: {threadId}]");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.Message}");
    Console.ResetColor();
}
