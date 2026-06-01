using System.Text.Json;
using HarAnalyzer.Models;

namespace HarAnalyzer;

/// <summary>
/// Reads HAR files in a memory-efficient streaming manner.
/// The file is loaded as raw bytes, parsed into a JsonDocument, and entries
/// are yielded one at a time without deserializing all of them into objects.
/// </summary>
public static class HarReader
{
    /// <summary>
    /// Yields HAR entries one by one from a file.
    /// The file is fully read into a buffer, but entries are yielded as standalone
    /// JsonElements — only the entry being processed is held in memory at a time.
    /// </summary>
    public static IAsyncEnumerable<(JsonElement entry, int index)> StreamEntriesAsync(
        string filePath,
        CancellationToken ct = default)
    {
        return StreamEntriesImpl(filePath, ct);
    }

    private static async IAsyncEnumerable<(JsonElement entry, int index)> StreamEntriesImpl(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Read entire file into buffer
        byte[] data;
        await using (var stream = File.OpenRead(filePath))
        {
            if (stream.Length > int.MaxValue)
                throw new InvalidOperationException("File too large (>2GB) for current implementation.");

            data = new byte[stream.Length];
            var bytesRead = 0;
            while (bytesRead < data.Length)
            {
                var n = await stream.ReadAsync(data.AsMemory(bytesRead), ct);
                if (n == 0) break;
                bytesRead += n;
            }
        }

        var options = new JsonDocumentOptions { AllowTrailingCommas = true, MaxDepth = 128 };
        using var doc = JsonDocument.Parse(data, options);

        // Navigate to log.entries
        if (!doc.RootElement.TryGetProperty("log", out var log))
            yield break;
        if (!log.TryGetProperty("entries", out var entries))
            yield break;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            yield return (entry, index);
            index++;
        }
    }

    /// <summary>
    /// Extracts a lightweight summary from a single entry JsonElement.
    /// Preserves the Raw element for detailed access.
    /// </summary>
    public static HarEntrySummary ParseEntrySummary(JsonElement entry, int id)
    {
        var request = entry.TryGetProperty("request", out var req) ? req : default;
        var response = entry.TryGetProperty("response", out var resp) ? resp : default;
        var time = entry.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : 0;

        var url = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
        var method = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
        var status = response.ValueKind != JsonValueKind.Undefined && response.TryGetProperty("status", out var s) ? s.GetInt32() : 0;
        var statusText = response.ValueKind != JsonValueKind.Undefined && response.TryGetProperty("statusText", out var st) ? st.GetString() : null;

        string? mimeType = null;
        if (response.ValueKind != JsonValueKind.Undefined && response.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Object && content.TryGetProperty("mimeType", out var mt))
        {
            mimeType = mt.GetString();
        }

        var requestBodySize = request.ValueKind != JsonValueKind.Undefined && request.TryGetProperty("bodySize", out var rbs) ? rbs.GetInt64() : 0;
        var responseBodySize = response.ValueKind != JsonValueKind.Undefined && response.TryGetProperty("bodySize", out var rpbs) ? rpbs.GetInt64() : 0;
        var transferSize = response.ValueKind != JsonValueKind.Undefined && response.TryGetProperty("_transferSize", out var ts) ? ts.GetInt64() : responseBodySize;
        var serverIP = entry.TryGetProperty("serverIPAddress", out var sip) ? sip.GetString() : null;

        string? domain = null;
        string? urlPath = null;
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var uri = new Uri(url);
                domain = uri.Host;
                urlPath = uri.PathAndQuery;
            }
            catch { domain = url; }
        }

        return new HarEntrySummary
        {
            Id = id,
            StartedDateTime = entry.TryGetProperty("startedDateTime", out var sdt) ? sdt.GetString() : null,
            TimeMs = time,
            Method = method,
            Url = url,
            Status = status,
            StatusText = statusText,
            MimeType = mimeType,
            RequestBodySize = requestBodySize,
            ResponseBodySize = responseBodySize,
            TransferSize = transferSize,
            ServerIPAddress = serverIP,
            Domain = domain,
            UrlPath = urlPath,
        };
    }

    /// <summary>
    /// Computes a full summary of the HAR file by scanning all entries.
    /// </summary>
    public static async Task<HarSummary> ComputeSummaryAsync(string filePath, CancellationToken ct = default)
    {
        int total = 0;
        long totalReqBytes = 0, totalRespBytes = 0;
        var statusCodes = new Dictionary<int, int>();
        var domains = new Dictionary<string, int>();
        var contentTypes = new Dictionary<string, int>();
        var timings = new List<double>();

        byte[] data;
        await using (var stream = File.OpenRead(filePath))
        {
            if (stream.Length > int.MaxValue)
                throw new InvalidOperationException("File too large (>2GB).");
            data = new byte[stream.Length];
            var bytesRead = 0;
            while (bytesRead < data.Length)
            {
                var n = await stream.ReadAsync(data.AsMemory(bytesRead), ct);
                if (n == 0) break;
                bytesRead += n;
            }
        }

        var options = new JsonDocumentOptions { AllowTrailingCommas = true, MaxDepth = 128 };
        using var doc = JsonDocument.Parse(data, options);

        if (!doc.RootElement.TryGetProperty("log", out var log))
            return new HarSummary();

        // Read metadata
        var version = log.TryGetProperty("version", out var v) ? v.GetString() : null;
        var pageCount = 0;
        if (log.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
        {
            pageCount = pages.GetArrayLength();
        }

        // Scan entries
        if (!log.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return new HarSummary { Version = version, PageCount = pageCount };

        total = entries.GetArrayLength();
        foreach (var entry in entries.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            if (entry.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.Number)
                timings.Add(t.GetDouble());
            if (entry.TryGetProperty("request", out var req))
            {
                if (req.TryGetProperty("bodySize", out var bs)) totalReqBytes += bs.GetInt64();
                if (req.TryGetProperty("url", out var urlStr))
                {
                    var url = urlStr.GetString() ?? "";
                    try { var uri = new Uri(url); var h = uri.Host; domains[h] = domains.GetValueOrDefault(h) + 1; } catch { }
                }
            }
            if (entry.TryGetProperty("response", out var resp))
            {
                if (resp.TryGetProperty("status", out var s))
                {
                    var sc = s.GetInt32();
                    statusCodes[sc] = statusCodes.GetValueOrDefault(sc) + 1;
                }
                if (resp.TryGetProperty("bodySize", out var rbs)) totalRespBytes += rbs.GetInt64();
                if (resp.TryGetProperty("content", out var cnt) && cnt.TryGetProperty("mimeType", out var mt))
                {
                    var mime = mt.GetString() ?? "unknown";
                    contentTypes[mime] = contentTypes.GetValueOrDefault(mime) + 1;
                }
            }
        }

        timings.Sort();
        var timingStats = new TimingStats
        {
            MinMs = timings.Count > 0 ? timings[0] : 0,
            MaxMs = timings.Count > 0 ? timings[^1] : 0,
            AvgMs = timings.Count > 0 ? timings.Average() : 0,
            MedianMs = timings.Count > 0 ? timings[timings.Count / 2] : 0
        };

        return new HarSummary
        {
            Version = version,
            Creator = null,
            TotalEntries = total,
            TotalRequestBytes = totalReqBytes,
            TotalResponseBytes = totalRespBytes,
            StatusCodeDistribution = statusCodes,
            TopDomains = domains.OrderByDescending(kv => kv.Value).Take(20).Select(kv => $"{kv.Key} ({kv.Value})").ToList(),
            Timings = timingStats,
            TopContentTypes = contentTypes.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key} ({kv.Value})").ToList(),
            PageCount = pageCount,
        };
    }
}
