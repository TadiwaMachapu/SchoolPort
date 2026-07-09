using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

/// <summary>Thrown when Gemini:ApiKey is missing, blank, or still the appsettings placeholder.
/// Callers map this to their "not_configured" outcome.</summary>
public sealed class GeminiNotConfiguredException : Exception
{
    public GeminiNotConfiguredException()
        : base("Gemini:ApiKey is not configured (set it via user-secrets).") { }
}

/// <summary>Thrown when the Gemini API returns a non-success status. Callers map this to their
/// "api_error" outcome and must NOT consume any user quota for the failed call.</summary>
public sealed class GeminiException : Exception
{
    public int StatusCode { get; }
    public GeminiException(int statusCode)
        : base($"Gemini API returned {statusCode}.") => StatusCode = statusCode;
}

public interface IGeminiService
{
    /// <summary>
    /// Calls gemini-1.5-flash with a system prompt + user message and returns the generated text
    /// (candidates[0].content.parts[0].text). Returns null when the response carries no usable
    /// candidate (safety block / empty). Throws <see cref="GeminiNotConfiguredException"/> when no
    /// key is configured and <see cref="GeminiException"/> on a non-200 response.
    /// </summary>
    Task<string?> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}

/// <summary>
/// Google Gemini REST caller (free tier — no NuGet client, plain HttpClient). All AI features
/// (Matric tutor, gap analysis, smart reports, Gr9 advisor) route through this one service so the
/// endpoint shape, model, and response parsing live in exactly one place.
/// </summary>
/// <summary>Helpers shared by the structured AI services (gap analysis, smart reports, Gr9
/// advisor), whose prompts ask the model for a JSON object embedded in free text.</summary>
public static class GeminiJson
{
    /// <summary>Extracts the outermost {...} block from model output; null when absent.</summary>
    public static string? ExtractObject(string? text)
    {
        if (text == null) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start < 0 || end <= start ? null : text[start..(end + 1)];
    }
}

public class GeminiService : IGeminiService
{
    public const string HttpClientName = "gemini";

    /// <summary>Neutral system prompt for the structured services — their full prompts (role,
    /// data, format) are unchanged from the Anthropic version and travel as the user message.</summary>
    public const string StructuredSystemPrompt =
        "Follow the instructions in the message and respond in exactly the requested format.";
    // Google keeps retiring/gating model names (1.5 retired; 2.5-flash 404s "no longer available
    // to new users" on this key; 3.5-flash was 503-overloaded when verified 2026-07-10), so the
    // model is config-driven (Gemini:Model) rather than hardcoded. The default is the model
    // verified working on this project's key.
    private const string DefaultModel = "gemini-3.1-flash-lite";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string Placeholder = "CHANGE_ME_USE_USER_SECRETS";

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<GeminiService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == Placeholder)
            throw new GeminiNotConfiguredException();

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userMessage } } } },
            generationConfig = new { maxOutputTokens = 1024, temperature = 0.7 },
        };

        var model = _config["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(model)) model = DefaultModel;

        // The key travels in the query string (Gemini convention) — never log the URL.
        var url = $"{BaseUrl}/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var response = await client.PostAsync(url,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API returned {Status}", (int)response.StatusCode);
            throw new GeminiException((int)response.StatusCode);
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // A 200 can still carry no usable candidate (safety block, recitation, empty) —
        // guard every hop rather than assume the happy shape.
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
        {
            _logger.LogWarning("Gemini response contained no candidates");
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array || parts.GetArrayLength() == 0 ||
            !parts[0].TryGetProperty("text", out var textEl))
        {
            _logger.LogWarning("Gemini candidate carried no text part");
            return null;
        }

        var text = textEl.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
