using System.Text.Json.Serialization;
using HarAnalyzer.Models;

namespace HarAnalyzer;

/// <summary>
/// Source-generated JSON serialization context for AOT trimming compatibility.
/// All types passed to JsonSerializer.Serialize must be registered here.
/// </summary>
[JsonSerializable(typeof(HarSummary))]
[JsonSerializable(typeof(HarEntrySummary))]
[JsonSerializable(typeof(DomainStats))]
[JsonSerializable(typeof(List<HarEntrySummary>))]
[JsonSerializable(typeof(List<DomainStats>))]
[JsonSerializable(typeof(List<EntryOutput>))]
[JsonSerializable(typeof(EntryOutput))]
[JsonSerializable(typeof(ShowOutput))]
public partial class HarJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Named type replacing anonymous type in list/errors output.
/// </summary>
public record EntryOutput
{
    public int Id { get; init; }
    public string? StartedDateTime { get; init; }
    public double TimeMs { get; init; }
    public string Method { get; init; } = "";
    public string Url { get; init; } = "";
    public int Status { get; init; }
    public string? StatusText { get; init; }
    public string? MimeType { get; init; }
    public long RequestSize { get; init; }
    public long ResponseSize { get; init; }
    public long TransferSize { get; init; }
    public string? ServerIPAddress { get; init; }
    public string? Domain { get; init; }

    public static EntryOutput From(HarEntrySummary e) => new()
    {
        Id = e.Id,
        StartedDateTime = e.StartedDateTime,
        TimeMs = e.TimeMs,
        Method = e.Method,
        Url = e.Url,
        Status = e.Status,
        StatusText = e.StatusText,
        MimeType = e.MimeType,
        RequestSize = e.RequestBodySize,
        ResponseSize = e.ResponseBodySize,
        TransferSize = e.TransferSize,
        ServerIPAddress = e.ServerIPAddress,
        Domain = e.Domain,
    };
}

/// <summary>
/// Named type for show output (id + raw entry JSON).
/// </summary>
public record ShowOutput
{
    public int Id { get; init; }
    public System.Text.Json.JsonElement Entry { get; init; }
}
