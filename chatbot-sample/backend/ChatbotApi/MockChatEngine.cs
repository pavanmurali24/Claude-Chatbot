using System.Runtime.CompilerServices;

namespace ChatbotApi;

/// <summary>
/// Stand-in for a real language model: a few keyword rules, then an echo fallback.
/// Swap this class for an HTTP call to a model provider and the rest of the sample
/// (endpoints, streaming, frontend) keeps working unchanged.
/// </summary>
public class MockChatEngine
{
    public const string ModelName = "mock-echo-1";

    private static readonly (string[] Keywords, string Reply)[] Rules =
    [
        (["hello", "hi ", "hey", "good morning", "good evening"],
            "Hello! I'm a mock chatbot running on ASP.NET Core and .NET 10. Ask me anything — I reply with canned rules, not a real model."),

        (["who are you", "what are you", "your name"],
            "I'm the sample bot for this project. The Streamlit page you're typing into talks to a .NET minimal API, which hands your message to a rule-based engine."),

        (["what can you do", "help", "capabilities"],
            "Three things, honestly: I greet you, I answer a handful of keyword questions, and I echo everything else back with a word count. Try \"tell me a joke\", \"what time is it\", or \"how does this work\"."),

        (["how does this work", "architecture", "how are you built"],
            "Streamlit (port 8501) collects your message and POSTs the full conversation to POST /api/chat/stream on the backend (port 5179). The backend streams the reply back as server-sent events, one token at a time, and Streamlit renders it as it arrives."),

        (["joke", "funny"],
            "A SQL query walks into a bar, approaches two tables, and asks: \"Mind if I join you?\""),

        (["time", "date", "today"],
            "I can read the server clock, but not much else: it is currently {TIME}."),

        (["bye", "goodbye", "thanks", "thank you"],
            "Any time. Refresh the page or hit \"Clear chat\" in the sidebar to start over."),
    ];

    /// <summary>Produces the whole reply at once.</summary>
    public string Reply(IReadOnlyList<ChatMessage> messages)
    {
        var lastUserMessage = messages.LastOrDefault(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

        if (lastUserMessage is null || string.IsNullOrWhiteSpace(lastUserMessage.Content))
            return "I didn't catch a message in there. Type something and I'll respond.";

        var text = lastUserMessage.Content.Trim();

        // Pad so a rule like "hi " can require a word boundary without regex.
        var haystack = $" {text.ToLowerInvariant()} ";
        foreach (var (keywords, reply) in Rules)
        {
            if (keywords.Any(haystack.Contains))
                return reply.Replace("{TIME}", DateTimeOffset.Now.ToString("dddd d MMMM yyyy, HH:mm"));
        }

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var turn = messages.Count(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        return $"You said: \"{text}\" — that's {wordCount} word{(wordCount == 1 ? "" : "s")}, "
             + $"and it's your {Ordinal(turn)} message this session. "
             + "A real model would answer properly here; I just echo.";
    }

    /// <summary>
    /// Same reply, emitted word by word with a small delay so the frontend can
    /// demonstrate incremental rendering the way a real model stream would.
    /// </summary>
    public async IAsyncEnumerable<string> StreamReply(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var words = Reply(messages).Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i == 0 ? words[i] : " " + words[i];
            await Task.Delay(35, cancellationToken);
        }
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ when n % 10 == 1 && n % 100 != 11 => $"{n}st",
        _ when n % 10 == 2 && n % 100 != 12 => $"{n}nd",
        _ when n % 10 == 3 && n % 100 != 13 => $"{n}rd",
        _ => $"{n}th",
    };
}
