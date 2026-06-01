using System.Text.Json;

namespace HarAnalyzer.Models;

/// <summary>
/// Lightweight models representing key fields in a HAR 1.2 entry.
/// Only the fields needed for analysis are included.
/// </summary>
public record HarSummary
{
    public string? Version { get; init; }
    public string? Creator { get; init; }
    public int TotalEntries { get; init; }
    public long TotalRequestBytes { get; init; }
    public long TotalResponseBytes { get; init; }
    public Dictionary<int, int> StatusCodeDistribution { get; init; } = new();
    public List<string> TopDomains { get; init; } = new();
    public TimingStats Timings { get; init; } = new();
    public List<string> TopContentTypes { get; init; } = new();
    public int PageCount { get; init; }
}

public record TimingStats
{
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double AvgMs { get; init; }
    public double MedianMs { get; init; }
}

/// <summary>
/// A single HAR entry with only the fields relevant for listing/searching.
/// For full detail, use the raw JSON via --raw flag on show command.
/// </summary>
public record HarEntrySummary
{
    public int Id { get; init; }
    public string? StartedDateTime { get; init; }
    public double TimeMs { get; init; }
    public string Method { get; init; } = "";
    public string Url { get; init; } = "";
    public int Status { get; init; }
    public string? StatusText { get; init; }
    public string? MimeType { get; init; }
    public long RequestBodySize { get; init; }
    public long ResponseBodySize { get; init; }
    public long TransferSize { get; init; }
    public string? ServerIPAddress { get; init; }
    public string? Domain { get; init; }
    public string? UrlPath { get; init; }

    public bool IsError => Status >= 400;
}

public record DomainStats
{
    public string Domain { get; init; } = "";
    public int RequestCount { get; init; }
    public long TotalResponseBytes { get; init; }
    public int ErrorCount { get; init; }
    public double AvgTimeMs { get; init; }
}
