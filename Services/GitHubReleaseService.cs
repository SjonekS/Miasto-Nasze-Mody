using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AmongUsModManager.Models;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public partial class GitHubReleaseService : IGitHubReleaseService
{
    private const string RepoOwner = "SjonekS";
    private const string RepoName = "Miasto-Nasze-Mody";
    private const string GitHubApiLatestReleaseUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string GitHubWebLatestReleaseUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly SocketsHttpHandler HttpHandler = new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        ConnectTimeout = TimeSpan.FromSeconds(20)
    };

    private static readonly HttpClient HttpClient = new(HttpHandler, disposeHandler: false)
    {
        Timeout = TimeSpan.FromMinutes(15),
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
    };

    [GeneratedRegex(@"v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"href=""(/[^""]+/releases/download/[^""]+TouMira[^""]+\.zip)""", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlAssetRegex();

    public async Task<GitHubReleaseInfo?> GetLatestTouMiraReleaseAsync(Action<string>? logCallback = null, CancellationToken ct = default)
    {
        logCallback?.Invoke("Sprawdzanie API GitHub (releases/latest)...");

        // 1. Try GitHub REST API
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiLatestReleaseUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AmongUsModManager", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            logCallback?.Invoke($"Odpowiedź GitHub API: HTTP {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                var tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                DateTimeOffset? publishedAt = null;
                if (root.TryGetProperty("published_at", out var pubElem) && DateTimeOffset.TryParse(pubElem.GetString(), out var dt))
                {
                    publishedAt = dt;
                }

                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    var candidateAssets = new List<(string Name, string DownloadUrl, long Size)>();
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                        long size = asset.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;

                        if (name.Contains("TouMira", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(url))
                        {
                            candidateAssets.Add((name, url, size));
                        }
                    }

                    if (candidateAssets.Count > 0)
                    {
                        var chosen = candidateAssets.First();
                        var ver = ParseSemanticVersion(tagName, chosen.Name);
                        if (ver.HasValue)
                        {
                            logCallback?.Invoke($"Wykryto wydanie przez API: {ver.Value.RawVersionString} ({chosen.Name})");
                            return new GitHubReleaseInfo
                            {
                                TagName = tagName,
                                Version = ver.Value.Version,
                                VersionString = ver.Value.RawVersionString,
                                AssetName = chosen.Name,
                                DownloadUrl = chosen.DownloadUrl,
                                SizeBytes = chosen.Size,
                                PublishedAt = publishedAt
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"Błąd zapytania API ({ex.GetType().Name}): {ex.Message}");
        }

        // 2. Fallback: Web Release Resolution (Zero Rate Limit)
        logCallback?.Invoke("Próba odpytania bezpośredniego punktu przekierowań GitHub Releases...");
        try
        {
            using var webRequest = new HttpRequestMessage(HttpMethod.Get, GitHubWebLatestReleaseUrl);
            webRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("AmongUsModManager", "1.0"));

            using var webResponse = await HttpClient.SendAsync(webRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            var finalUri = webResponse.RequestMessage?.RequestUri?.ToString() ?? "";
            logCallback?.Invoke($"Przekierowano na adres: {finalUri}");

            // Extract tag from URL: https://github.com/owner/repo/releases/tag/v1.7.3
            var tagMatch = Regex.Match(finalUri, @"/releases/tag/([^/?#]+)", RegexOptions.IgnoreCase);
            if (tagMatch.Success)
            {
                var tagName = Uri.UnescapeDataString(tagMatch.Groups[1].Value);
                var html = await webResponse.Content.ReadAsStringAsync(ct);

                // Find asset download href
                var assetMatch = HtmlAssetRegex().Match(html);
                if (assetMatch.Success)
                {
                    var relUrl = assetMatch.Groups[1].Value;
                    var fullDownloadUrl = "https://github.com" + relUrl;
                    var assetName = Path.GetFileName(relUrl);

                    var ver = ParseSemanticVersion(tagName, assetName);
                    if (ver.HasValue)
                    {
                        logCallback?.Invoke($"Wykryto wydanie przez stronę web: {ver.Value.RawVersionString} ({assetName})");
                        return new GitHubReleaseInfo
                        {
                            TagName = tagName,
                            Version = ver.Value.Version,
                            VersionString = ver.Value.RawVersionString,
                            AssetName = assetName,
                            DownloadUrl = fullDownloadUrl,
                            SizeBytes = 0,
                            PublishedAt = DateTimeOffset.Now
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"Błąd metody fallback web: {ex.Message}");
        }

        logCallback?.Invoke("Nie udało się odnaleźć odpowiedniego wydania TouMira na GitHub.");
        return null;
    }

    private static (Version Version, string RawVersionString)? ParseSemanticVersion(string tagName, string assetName)
    {
        var match = VersionRegex().Match(tagName);
        if (!match.Success)
        {
            match = VersionRegex().Match(assetName);
        }

        if (!match.Success) return null;

        int major = int.Parse(match.Groups["major"].Value);
        int minor = int.Parse(match.Groups["minor"].Value);
        int patch = int.Parse(match.Groups["patch"].Value);
        var version = new Version(major, minor, patch);
        var rawVersion = $"v{major}.{minor}.{patch}";

        return (version, rawVersion);
    }

    public async Task<string> DownloadAssetAsync(
        GitHubReleaseInfo release,
        string destinationDirectory,
        IProgress<OperationProgress>? progress = null,
        Action<string>? logCallback = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var destinationFile = Path.Combine(destinationDirectory, release.AssetName);
        var tempFile = destinationFile + ".download";

        const int maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            logCallback?.Invoke($"[Próba {attempt}/{maxAttempts}] Pobieranie z: {release.DownloadUrl}");

            try
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AmongUsModManager", "1.0"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                var finalHost = response.RequestMessage?.RequestUri?.Host ?? "GitHub";
                logCallback?.Invoke($"[Próba {attempt}/{maxAttempts}] Status HTTP: {(int)response.StatusCode} {response.StatusCode} (Host: {finalHost})");

                response.EnsureSuccessStatusCode();

                long? reportedLength = response.Content.Headers.ContentLength;
                long totalBytes = reportedLength ?? (release.SizeBytes > 0 ? release.SizeBytes : 0);

                if (totalBytes > 0)
                {
                    logCallback?.Invoke($"[Próba {attempt}/{maxAttempts}] Rozmiar oczekiwany: {totalBytes / (1024.0 * 1024.0):0.0} MB");
                }

                using (var src = await response.Content.ReadAsStreamAsync(ct))
                using (var dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;

                    while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        await dest.WriteAsync(buffer, 0, read, ct);
                        totalRead += read;

                        double mbRead = totalRead / (1024.0 * 1024.0);

                        if (totalBytes > 0)
                        {
                            double percentage = Math.Min(100.0, (double)totalRead / totalBytes * 100.0);
                            double mbTotal = totalBytes / (1024.0 * 1024.0);

                            progress?.Report(new OperationProgress(
                                percentage,
                                $"Pobieranie TouMira {release.VersionString} ({percentage:0}%): {mbRead:0.0} MB / {mbTotal:0.0} MB"
                            ));
                        }
                        else
                        {
                            progress?.Report(new OperationProgress(
                                50.0,
                                $"Pobieranie TouMira {release.VersionString}: {mbRead:0.0} MB"
                            ));
                        }
                    }

                    await dest.FlushAsync(ct);
                }

                stopwatch.Stop();

                var fi = new FileInfo(tempFile);
                if (fi.Length == 0)
                {
                    throw new IOException("Pobrany plik tymczasowy ma 0 bajtów.");
                }

                if (totalBytes > 0 && fi.Length < (totalBytes * 0.95))
                {
                    throw new IOException($"Pobrano niekompletny plik: {fi.Length} bajtów z oczekiwanych {totalBytes} bajtów.");
                }

                if (File.Exists(destinationFile))
                {
                    File.Delete(destinationFile);
                }

                File.Move(tempFile, destinationFile);

                logCallback?.Invoke($"[Sukces] Pobrano plik {release.AssetName} ({fi.Length / (1024.0 * 1024.0):0.0} MB) w {stopwatch.Elapsed.TotalSeconds:0.1} s.");
                return destinationFile;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                stopwatch.Stop();
                lastException = ex;

                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }

                logCallback?.Invoke($"[Próba {attempt}/{maxAttempts}] Błąd pobierania ({ex.GetType().Name}): {ex.Message}");

                if (attempt < maxAttempts)
                {
                    int delayMs = 1500 * attempt;
                    logCallback?.Invoke($"Oczekiwanie {delayMs / 1000.0:0.0} s przed ponowną próbą...");
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        if (File.Exists(tempFile))
        {
            try { File.Delete(tempFile); } catch { }
        }

        throw new InvalidOperationException(
            $"Pobieranie pliku '{release.AssetName}' z GitHub Releases nie powiodło się po {maxAttempts} próbach.\n\n" +
            $"Ostatni błąd: {lastException?.Message}\n\n" +
            $"Sprawdź połączenie z internetem lub użyj opcji 'Wybierz ZIP ręcznie'.",
            lastException);
    }
}
