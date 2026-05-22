namespace SchoolPortal.Server.Services;

public interface IStorageService
{
    Task<(string Url, string FileName)> UploadSubmissionFileAsync(
        Guid schoolId, Guid assignmentId, Guid studentId,
        IFormFile file, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StorageService> _logger;
    private readonly HttpClient _http;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
        ".txt", ".md", ".zip", ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mp3"
    };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public StorageService(IConfiguration config, ILogger<StorageService> logger, IHttpClientFactory factory)
    {
        _config = config;
        _logger = logger;
        _http = factory.CreateClient("supabase-storage");
    }

    public async Task<(string Url, string FileName)> UploadSubmissionFileAsync(
        Guid schoolId, Guid assignmentId, Guid studentId,
        IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0)
            throw new ArgumentException("File is empty");

        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException("File exceeds the 50 MB limit");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed");

        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName)
            .Replace(" ", "_")
            .Replace("..", "")
            .Substring(0, Math.Min(50, Path.GetFileNameWithoutExtension(file.FileName).Length));

        var objectPath = $"submissions/{schoolId}/{assignmentId}/{studentId}/{Guid.NewGuid()}_{safeFileName}{ext}";

        var supabaseUrl = _config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url not configured");
        var supabaseKey = _config["Supabase:ServiceRoleKey"] ?? throw new InvalidOperationException("Supabase:ServiceRoleKey not configured");
        var bucket = _config["Supabase:StorageBucket"] ?? "submissions";

        using var content = new StreamContent(file.OpenReadStream());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            file.ContentType ?? "application/octet-stream");

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{supabaseUrl}/storage/v1/object/{bucket}/{objectPath}");
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Add("Authorization", $"Bearer {supabaseKey}");
        request.Content = content;

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Supabase storage upload failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("File upload failed");
        }

        var publicUrl = $"{supabaseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
        return (publicUrl, file.FileName);
    }
}
