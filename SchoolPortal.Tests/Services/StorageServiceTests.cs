using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SchoolPortal.Server.Services;
using Xunit;

namespace SchoolPortal.Tests.Services;

/// <summary>
/// Sprint 1.5.0.6 (POPIA): the submissions bucket is private; StorageService mints
/// short-lived signed URLs via the Supabase Storage REST API. These tests pin down
/// the sign-request shape, response parsing, legacy public-URL normalisation, and
/// the never-500 failure contract (signing failure → null, callers render
/// "file unavailable"). HTTP is mocked — the live 400/403-without-signature check
/// is part of the sprint's live spot-check, not this suite.
/// </summary>
public class StorageServiceTests
{
    private const string SupabaseUrl = "https://example.supabase.co";
    private const string Bucket = "submissions";

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = SupabaseUrl,
            ["Supabase:ServiceRoleKey"] = "service-role-key",
            ["Supabase:StorageBucket"] = Bucket,
        })
        .Build();

    /// <summary>StorageService with a scripted HTTP handler; captures the outbound request.</summary>
    private static StorageService ServiceFor(
        HttpStatusCode status, string responseBody,
        Action<HttpRequestMessage, string?>? captureRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken _) =>
            {
                var body = req.Content is null ? null : await req.Content.ReadAsStringAsync();
                captureRequest?.Invoke(req, body);
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(responseBody),
                };
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("supabase-storage"))
            .Returns(new HttpClient(handler.Object));

        return new StorageService(Config(), NullLogger<StorageService>.Instance, factory.Object);
    }

    // ---- ExtractObjectPath: stored-value normalisation ----

    [Theory]
    // New rows (1.5.0.6+) store the bucket-relative object path directly.
    [InlineData("submissions/school/assignment/student/file.pdf",
                "submissions/school/assignment/student/file.pdf")]
    [InlineData("/submissions/school/file.pdf", "submissions/school/file.pdf")]
    // Legacy rows store the full public URL — strip "{url}/storage/v1/object/public/{bucket}/".
    [InlineData(SupabaseUrl + "/storage/v1/object/public/submissions/submissions/s/a/st/file.pdf",
                "submissions/s/a/st/file.pdf")]
    // Defensive: query string dropped.
    [InlineData(SupabaseUrl + "/storage/v1/object/public/submissions/sub/file.pdf?download=1",
                "sub/file.pdf")]
    public void ExtractObjectPath_NormalisesKnownShapes(string stored, string expected)
    {
        Assert.Equal(expected, StorageService.ExtractObjectPath(stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    // Absolute URL that is not the known public-object shape → cannot derive a path.
    [InlineData("https://evil.example.com/some/file.pdf")]
    [InlineData(SupabaseUrl + "/storage/v1/object/sign/submissions/x.pdf?token=abc")]
    // Marker present but nothing after the bucket segment.
    [InlineData(SupabaseUrl + "/storage/v1/object/public/submissions/")]
    public void ExtractObjectPath_ReturnsNullForUnknownShapes(string stored)
    {
        Assert.Null(StorageService.ExtractObjectPath(stored));
    }

    // ---- GetSignedUrlsAsync: request shape + response parsing ----

    [Fact]
    public async Task GetSignedUrls_BuildsBulkSignRequest_AndMapsResponseByOriginalInput()
    {
        var legacyUrl = $"{SupabaseUrl}/storage/v1/object/public/{Bucket}/submissions/s1/a1/st1/old.pdf";
        var newPath = "submissions/s2/a2/st2/new.pdf";

        HttpRequestMessage? sent = null;
        string? sentBody = null;
        var svc = ServiceFor(HttpStatusCode.OK, $$"""
            [
              { "path": "submissions/s1/a1/st1/old.pdf", "signedURL": "/object/sign/{{Bucket}}/submissions/s1/a1/st1/old.pdf?token=tok1" },
              { "path": "{{newPath}}", "signedURL": "/object/sign/{{Bucket}}/{{newPath}}?token=tok2" }
            ]
            """, (req, body) => { sent = req; sentBody = body; });

        var result = await svc.GetSignedUrlsAsync(new[] { legacyUrl, newPath }, expirySeconds: 900);

        // Request: POST {url}/storage/v1/object/sign/{bucket} with service-role auth.
        Assert.NotNull(sent);
        Assert.Equal(HttpMethod.Post, sent!.Method);
        Assert.Equal($"{SupabaseUrl}/storage/v1/object/sign/{Bucket}", sent.RequestUri!.ToString());
        Assert.Equal("Bearer service-role-key", sent.Headers.GetValues("Authorization").Single());

        // Body: expiry honoured; legacy URL normalised to its object path before signing.
        using var doc = JsonDocument.Parse(sentBody!);
        Assert.Equal(900, doc.RootElement.GetProperty("expiresIn").GetInt32());
        var paths = doc.RootElement.GetProperty("paths").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(2, paths.Count);
        Assert.Contains("submissions/s1/a1/st1/old.pdf", paths);
        Assert.Contains(newPath, paths);

        // Result: keyed by ORIGINAL stored value; full absolute signed URLs.
        Assert.Equal(
            $"{SupabaseUrl}/storage/v1/object/sign/{Bucket}/submissions/s1/a1/st1/old.pdf?token=tok1",
            result[legacyUrl]);
        Assert.Equal(
            $"{SupabaseUrl}/storage/v1/object/sign/{Bucket}/{newPath}?token=tok2",
            result[newPath]);
    }

    [Fact]
    public async Task GetSignedUrls_PerObjectError_MapsThatEntryToNull()
    {
        var okPath = "submissions/s/a/st/ok.pdf";
        var gonePath = "submissions/s/a/st/deleted.pdf";

        var svc = ServiceFor(HttpStatusCode.OK, $$"""
            [
              { "path": "{{okPath}}", "signedURL": "/object/sign/{{Bucket}}/{{okPath}}?token=tok" },
              { "path": "{{gonePath}}", "error": "Not found", "signedURL": null }
            ]
            """);

        var result = await svc.GetSignedUrlsAsync(new[] { okPath, gonePath });

        Assert.NotNull(result[okPath]);
        Assert.Null(result[gonePath]);
    }

    [Fact]
    public async Task GetSignedUrls_HttpFailure_ReturnsAllNull_NeverThrows()
    {
        var svc = ServiceFor(HttpStatusCode.InternalServerError, "storage down");

        var result = await svc.GetSignedUrlsAsync(new[] { "submissions/s/a/st/file.pdf" });

        Assert.Null(result["submissions/s/a/st/file.pdf"]);
    }

    [Fact]
    public async Task GetSignedUrls_UnparseableStoredValue_MapsToNull_WithoutHttpCall()
    {
        var called = false;
        var svc = ServiceFor(HttpStatusCode.OK, "[]", (_, _) => called = true);

        var result = await svc.GetSignedUrlsAsync(new[] { "https://evil.example.com/file.pdf" });

        Assert.Null(result["https://evil.example.com/file.pdf"]);
        Assert.False(called); // nothing signable → no round-trip
    }

    [Fact]
    public async Task GetSignedUrlAsync_Single_WrapsBulk()
    {
        var path = "submissions/s/a/st/file.pdf";
        var svc = ServiceFor(HttpStatusCode.OK, $$"""
            [ { "path": "{{path}}", "signedURL": "/object/sign/{{Bucket}}/{{path}}?token=tok" } ]
            """);

        var signed = await svc.GetSignedUrlAsync(path);

        Assert.Equal($"{SupabaseUrl}/storage/v1/object/sign/{Bucket}/{path}?token=tok", signed);
    }

    // ---- Upload: private-bucket contract ----

    [Fact]
    public async Task Upload_ReturnsObjectPath_NotPublicUrl()
    {
        HttpRequestMessage? sent = null;
        var svc = ServiceFor(HttpStatusCode.OK, "{}", (req, _) => sent = req);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(10);
        file.Setup(f => f.FileName).Returns("essay.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[10]));

        var schoolId = Guid.NewGuid();
        var (path, name) = await svc.UploadSubmissionFileAsync(
            schoolId, Guid.NewGuid(), Guid.NewGuid(), file.Object);

        Assert.Equal("essay.pdf", name);
        // Sprint 1.5.0.6: stored value is the bucket-relative object path — no public URL.
        Assert.StartsWith($"submissions/{schoolId}/", path);
        Assert.DoesNotContain("://", path);
        Assert.DoesNotContain("/object/public/", path);
        // Upload target unchanged: POST {url}/storage/v1/object/{bucket}/{path}.
        Assert.StartsWith($"{SupabaseUrl}/storage/v1/object/{Bucket}/submissions/", sent!.RequestUri!.ToString());
    }
}
