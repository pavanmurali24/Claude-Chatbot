namespace ChatbotApi;

/// <summary>A single turn in the conversation. Role is "user" or "assistant".</summary>
public record ChatMessage(string Role, string Content);

/// <summary>
/// The frontend posts the whole conversation every time, so the backend stays stateless.
/// </summary>
public record ChatRequest(List<ChatMessage> Messages);

public record ChatResponse(string Reply, string Model, DateTimeOffset CreatedAt);
