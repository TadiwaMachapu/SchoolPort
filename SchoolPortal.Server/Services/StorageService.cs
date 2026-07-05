using System.Text;
using System.Text.Json;

namespace SchoolPortal.Server.Services;

public interface IStorageService
{
    /// <summary>Uploads a submission file and returns the bucket-relative object path
    /// (stored in Submission.FileUrl; reads mint signed URLs via GetSignedUrlAsync).</summary>
    Task<(string Path, string FileName)> UploadSubmissionFileAsync(
        Guid schoolId, Guid assignmentId, Guid studentId,
        IFormFile file, CancellationToken ct = default);

    /// <summary>Mints a short-lived signed URL for a private-bucket object (Sprint 1.5.0.6, POPIA).
    /// Accepts either a bucket-relative object path or a legacy full public URL (pre-1.5.0.6 rows).
    /// Returns null on signing failure — callers render "file unavailable", never 500.</summary>
    Task<string?> GetSignedUrlAsync(string path, int expirySeconds = 3600, CancellationToken ct = default);

    /// <summary>Bulk variant of GetSignedUrlAsync — one HTTP round-trip for a whole class's
    /// submissions. Returns a map keyed by the ORIGINAL input value (path or legacy URL);
    /// entries that failed to sign map to null.</summary>
    Task<Dictionary<string, string?>> GetSignedUrlsAsync(
        IReadOnlyCollection<string> paths, int expirySeconds = 3600, CancellationToken ct = default);
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

    private string SupabaseUrl => (_config["Supabase:Url"]
        ?? throw new InvalidOperationException("Supabase:Url not configured")).TrimEnd('/');

    private string SupabaseKey => _config["Supabase:ServiceRoleKey"]
        ?? throw new InvalidOperationException("Supabase:ServiceRoleKey not configured");

    private string Bucket => _config["Supabase:StorageBucket"] ?? "submissions";

    public async Task<(string Path, string FileName)> UploadSubmissionFileAsync(
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

        using var content = new StreamContent(file.OpenReadStream());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            file.ContentType ?? "application/octet-stream");

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{SupabaseUrl}/storage/v1/object/{Bucket}/{objectPath}");
        request.Headers.Add("apikey", SupabaseKey);
        request.Headers.Add("Authorization", $"Bearer {SupabaseKey}");
        request.Content = content;

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Supabase storage upload failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("File upload failed");
        }

        // Sprint 1.5.0.6: the bucket is private — store the object path, not a public URL.
        // Reads mint a signed URL at DTO-build time (SubmissionService).
        return (objectPath, file.FileName);
    }

    public async Task<string?> GetSignedUrlAsync(string path, int expirySeconds = 3600, CancellationToken ct = default)
    {
        var map = await GetSignedUrlsAsync(new[] { path }, expirySeconds, ct);
        return map.TryGetValue(path, out var signed) ? signed : null;
    }

    public async Task<Dictionary<string, string?>> GetSignedUrlsAsync(
        IReadOnlyCollection<string> paths, int expirySeconds = 3600, CancellationToken ct = default)
    {
        // Result is keyed by the ORIGINAL input (which may be a legacy full public URL),
        // so callers can map back without re-deriving object paths.
        var result = paths.Distinct().ToDictionary(p => p, _ => (string?)null);
        // objectPath → original inputs (two legacy URL spellings can normalise to one path)
        var pathToInputs = new Dictionary<string, List<string>>();
        foreach (var input in result.Keys)
        {
            var objectPath = ExtractObjectPath(input);
            if (objectPath is null)
            {
                _logger.LogWarning("Could not derive a storage object path from stored FileUrl value");
                continue;
            }
            if (!pathToInputs.TryGetValue(objectPath, out var list))
                pathToInputs[objectPath] = list = new List<string>();
            list.Add(input);
        }

        if (pathToInputs.Count == 0) return result;

        try
        {
            // Bulk sign: POST /storage/v1/object/sign/{bucket} { expiresIn, paths } →
            // [{ path, signedURL | error }] — one round-trip for a whole class list.
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{SupabaseUrl}/storage/v1/object/sign/{Bucket}");
            request.Headers.Add("apikey", SupabaseKey);
            request.Headers.Add("Authorization", $"Bearer {SupabaseKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { expiresIn = expirySeconds, paths = pathToInputs.Keys }),
                Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Supabase bulk sign failed: {Status} {Body}", response.StatusCode, body);
                return result;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("signedURL", out var signedProp) ||
                    signedProp.ValueKind != JsonValueKind.String)
                    continue; // per-object error (e.g. object deleted) → stays null

                var objectPath = item.GetProperty("path").GetString();
                if (objectPath is null || !pathToInputs.TryGetValue(objectPath, out var inputs))
                    continue;

                // signedURL is relative to /storage/v1 (e.g. "/object/sign/{bucket}/{path}?token=...")
                var full = $"{SupabaseUrl}/storage/v1{signedProp.GetString()}";
                foreach (var input in inputs)
                    result[input] = full;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            // Signing failure must not 500 a submissions list — callers render "file unavailable".
            _logger.LogError(ex, "Supabase signed URL generation failed");
        }

        return result;
    }

    /// <summary>Normalises a stored FileUrl value to a bucket-relative object path.
    /// New rows (1.5.0.6+) store the path directly; legacy rows store the full public URL
    /// "{supabaseUrl}/storage/v1/object/public/{bucket}/{path}". Returns null if the value
    /// is an absolute URL that doesn't match the known public-object shape.</summary>
    internal static string? ExtractObjectPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (!value.Contains("://"))
            return value.TrimStart('/'); // already a bucket-relative path

        const string marker = "/storage/v1/object/public/";
        var idx = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        // Strip "{bucket}/" after the marker to get the bucket-relative path.
        var afterMarker = value[(idx + marker.Length)..];
        var slash = afterMarker.IndexOf('/');
        if (slash <= 0 || slash == afterMarker.Length - 1) return null;

        var path = afterMarker[(slash + 1)..];
        // Drop any query string (defensive — public URLs shouldn't carry one).
        var q = path.IndexOf('?');
        return q >= 0 ? path[..q] : path;
    }
}
