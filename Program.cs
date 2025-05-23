using Microsoft.Extensions.AI;
using Azure;
using Azure.AI.Inference;
using Microsoft.AspNetCore.Mvc;
using ChatDemo.ViewModels;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
var endpoint = Environment.GetEnvironmentVariable("GITHUB_ENDPOINT");

var azureClient = new ChatCompletionsClient(new(endpoint!), new AzureKeyCredential(githubToken!))
    .AsIChatClient("gpt-4o-mini");

var client = new ChatClientBuilder(azureClient)
    .UseFunctionInvocation()
    .Build();

builder.Services.AddChatClient(client);

builder.Services.AddOpenApi();
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

app.UseStaticFiles();
app.UseDefaultFiles();

app.MapFallbackToFile("index.html");

app.UseHttpsRedirection();

// Endpoint to get initial welcome message and recommendations
app.MapGet("/welcome", () =>
{
    var welcome = new
    {
        message = "Welcome! How can I help you today?",
        recommendations = new[]
        {
            "Show me my profile",
            "Help with billing",
            "Contact support",
            "Upgrade my plan",
            "Show recent activity"
        }
    };
    return Results.Ok(welcome);
});

var chatOptions = new ChatOptions
{
    Tools = [
        AIFunctionFactory.Create((string location, string unit) =>
        {
            // Here you would call a weather API
            // to get the weather for the location.
            return "Periods of rain or drizzle, 15 C";
        },
        "get_current_weather",
        "Get the current weather in a given location"),
        AIFunctionFactory.Create((string location, string unit) =>
        {
            // Here you would call a weather API
            // to get the weather for the location.
            return "Periods of rain or drizzle, 15 C";
        },
        "get_forecast",
        "Get the weather forecast for a given location"),
        AIFunctionFactory.Create(() =>
        {
            // Here you would call a weather API
            // to get the weather for the location.
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        },
        "get_current_time",
        "Get the current time in UTC"),
    ]
};

app.MapPost("/chat", async (
    [FromServices] IChatClient chatClient,
    [FromServices] IDistributedCache cache,
    [FromBody] ChatRequest request,
    CancellationToken cancellationToken) =>
{
    var conversationId = request.ConversationId ?? string.Empty;
    var cacheKey = $"chat:{conversationId}";
    List<Message> chatHistory;

    // Try to get chat history from cache
    var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
    if (!string.IsNullOrEmpty(cached))
    {
        chatHistory = System.Text.Json.JsonSerializer.Deserialize<List<Message>>(cached) ?? [];
    }
    else
    {
        chatHistory = [];
    }

    // Add the system message if not already present
    if (!chatHistory.Any(m => m.Role == "System"))
    {
        var systemMsg = new Message
        {
            Role = "System",
            Content = "You are a helpful assistant."
        };
        chatHistory.Insert(0, systemMsg);
    }

    // Add the new user message if not null
    if (request.Message != null)
    {
        chatHistory.Add(request.Message);
    }

    // Convert to ChatMessage for AI client
    var chatMessages = chatHistory.Select(m => new ChatMessage(
        m.Role == "User" ? Microsoft.Extensions.AI.ChatRole.User : Microsoft.Extensions.AI.ChatRole.Assistant, m.Content)).ToList();

    // Get response
    var response = await chatClient.GetResponseAsync(chatMessages, chatOptions, cancellationToken: cancellationToken);

    // Add assistant response to history
    var assistantMsg = new Message { Role = "Assistant", Content = response.Text };
    chatHistory.Add(assistantMsg);

    // Save updated history to cache
    var serialized = System.Text.Json.JsonSerializer.Serialize(chatHistory);
    await cache.SetStringAsync(cacheKey, serialized, cancellationToken);

    return Results.Ok(assistantMsg);
})
.WithName("chat");

app.MapPost("/recommendations", ([FromBody] object payload) =>
{
    // For now, return a random subset of recommended actions
    var allRecommendations = new[]
    {
        "Show me my profile",
        "Help with billing",
        "Contact support",
        "Upgrade my plan",
        "Show recent activity",
        "Reset my password",
        "Find documentation",
        "Open settings",
        "Log out"
    };
    var rnd = new Random();
    var count = rnd.Next(2, 6); // Return between 2 and 5 items
    var shuffled = allRecommendations.OrderBy(_ => rnd.Next()).Take(count).ToArray();
    return Results.Ok(shuffled);
})
.WithName("recommendations");


app.Run();
