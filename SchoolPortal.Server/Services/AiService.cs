using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface IAiService
{
    Task<GradeSuggestion> SuggestGradeAsync(string assignmentTitle, string? assignmentDescription,
        decimal maxMarks, string submissionText, string? rubric = null);
    Task<List<string>> GenerateQuizQuestionsAsync(string lessonContent, int questionCount = 5);
    Task<double> CheckPlagiarismAsync(string submission1, string submission2);
}

public record GradeSuggestion(decimal SuggestedScore, string Feedback, string Reasoning, decimal Confidence);

public class AiService : IAiService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AiService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-6";

    public AiService(IConfiguration config, ILogger<AiService> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private async Task<string> CallClaudeAsync(string prompt, int maxTokens = 500)
    {
        var apiKey = _config["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey not configured");

        var client = _httpClientFactory.CreateClient("anthropic");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = Model,
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var json = JsonSerializer.Serialize(body);
        var response = await client.PostAsync(AnthropicApiUrl,
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    public async Task<GradeSuggestion> SuggestGradeAsync(
        string assignmentTitle, string? assignmentDescription,
        decimal maxMarks, string submissionText, string? rubric = null)
    {
        var prompt = "You are an experienced teacher grading a student submission.\n\n" +
            $"Assignment: {assignmentTitle}\n" +
            (assignmentDescription != null ? $"Description: {assignmentDescription}\n" : "") +
            $"Maximum Marks: {maxMarks}\n" +
            (rubric != null ? $"Rubric: {rubric}\n" : "") +
            $"\nStudent Submission:\n{submissionText}\n\n" +
            "Respond ONLY with this JSON (no other text):\n" +
            $"{{\"suggestedScore\": 0, \"feedback\": \"\", \"reasoning\": \"\", \"confidence\": 0.9}}\n" +
            $"suggestedScore must be between 0 and {maxMarks}.";

        try
        {
            var text = await CallClaudeAsync(prompt, 600);
            // Extract JSON from response
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text[start..(end + 1)];

            var result = JsonDocument.Parse(text).RootElement;
            return new GradeSuggestion(
                SuggestedScore: Math.Min(maxMarks, Math.Max(0, (decimal)result.GetProperty("suggestedScore").GetDouble())),
                Feedback: result.GetProperty("feedback").GetString() ?? "",
                Reasoning: result.GetProperty("reasoning").GetString() ?? "",
                Confidence: (decimal)result.GetProperty("confidence").GetDouble()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI grading failed");
            throw new InvalidOperationException("AI grading failed: " + ex.Message);
        }
    }

    public async Task<List<string>> GenerateQuizQuestionsAsync(string lessonContent, int questionCount = 5)
    {
        var content = lessonContent.Length > 3000 ? lessonContent[..3000] : lessonContent;
        var prompt = $"Create {questionCount} multiple-choice quiz questions from this lesson content:\n\n{content}\n\n" +
            "Format each question as: Q: [question]\nA) [option]\nB) [option]\nC) [option]\nD*) [correct option - mark with *]";

        var text = await CallClaudeAsync(prompt, 2000);
        return text.Split('\n').Where(l => l.TrimStart().StartsWith("Q:")).ToList();
    }

    public async Task<double> CheckPlagiarismAsync(string submission1, string submission2)
    {
        var s1 = submission1.Length > 1000 ? submission1[..1000] : submission1;
        var s2 = submission2.Length > 1000 ? submission2[..1000] : submission2;

        var prompt = $"Rate the similarity of these two student submissions from 0 (different) to 1 (identical).\n\n" +
            $"Submission 1:\n{s1}\n\nSubmission 2:\n{s2}\n\n" +
            "Respond with only a decimal number like 0.85. No other text.";

        var text = await CallClaudeAsync(prompt, 10);
        return double.TryParse(text.Trim(), out var score) ? Math.Clamp(score, 0, 1) : 0;
    }
}
