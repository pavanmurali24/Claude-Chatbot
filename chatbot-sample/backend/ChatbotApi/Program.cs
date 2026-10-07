using ChatbotApi;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

// Streamlit runs on its own origin (localhost:8501), so the browser needs CORS.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:8501"];

builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSingleton<MockChatEngine>();

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    model = MockChatEngine.ModelName,
    serverTime = DateTimeOffset.Now,
}));

// Non-streaming: one request, one complete reply. Easiest thing to curl.
app.MapPost("/api/chat", (ChatRequest request, MockChatEngine engine) =>
{
    if (request.Messages is null || request.Messages.Count == 0)
        return Results.BadRequest(new { error = "messages must contain at least one item" });

    var reply = engine.Reply(request.Messages);
    return Results.Ok(new ChatResponse(reply, MockChatEngine.ModelName, DateTimeOffset.Now));
});

// Streaming: server-sent events, one chunk per word. This is what the UI uses.
app.MapPost("/api/chat/stream", (ChatRequest request, MockChatEngine engine, CancellationToken ct) =>
{
    if (request.Messages is null || request.Messages.Count == 0)
        return Results.BadRequest(new { error = "messages must contain at least one item" });

    return TypedResults.ServerSentEvents(engine.StreamReply(request.Messages, ct), eventType: "token");
});

app.Run();
