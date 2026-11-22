using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace portfolio.Data;

public class ImageStorageService
{
    private const string BucketName = "Project_image";
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".webp", "image/webp" }
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _key;

    public ImageStorageService(HttpClient http, IConfiguration config)
    {
        _http = http;

        _baseUrl = (config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase URL missing."))
            .Trim().TrimEnd('/');

        _key = (config["Supabase:ServiceRoleKey"]
            ?? throw new InvalidOperationException("Supabase key missing."))
            .Trim();
    }

    // Returns an error message, or null if the file is fine.
    public string? Validate(IFormFile file)
    {
        if (file.Length > MaxFileSize)
            return $"{file.FileName} is larger than 5 MB.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!ContentTypes.TryGetValue(ext, out var type) ||
            file.ContentType != type)
            return $"{file.FileName} must be a JPG, PNG, or WEBP image.";

        return null;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("apikey", _key);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _key);
        return request;
    }

    // Uploads one file and returns its public URL.
    public async Task<string> UploadAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var path = $"{Guid.NewGuid():N}{ext}";

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        using var content = new ByteArrayContent(ms.ToArray());
        content.Headers.ContentType =
            new MediaTypeHeaderValue(ContentTypes[ext]);

        using var request = CreateRequest(
            HttpMethod.Post,
            $"{_baseUrl}/storage/v1/object/{BucketName}/{path}",
            content);

        request.Headers.Add("x-upsert", "false");

        using var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Storage upload failed ({(int)response.StatusCode}): {body}");
        }

        return $"{_baseUrl}/storage/v1/object/public/{BucketName}/{path}";
    }

    // Removes the file behind a public URL. Never throws.
    public async Task DeleteByUrlAsync(string url)
    {
        try
        {
            var marker = $"/{BucketName}/";
            var index = url.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return;

            var path = url[(index + marker.Length)..];

            using var request = CreateRequest(
                HttpMethod.Delete,
                $"{_baseUrl}/storage/v1/object/{BucketName}/{path}");

            using var response = await _http.SendAsync(request);
        }
        catch
        {
            // Ignore storage cleanup problems.
        }
    }
}