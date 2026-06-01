using System.Text.Json;
using System.Text.Json.Serialization;
using HarAnalyzer.Models;

namespace HarAnalyzer;

/// <summary>
/// Formats analysis results as JSON or human-readable tables.
/// Uses source-generated JsonSerializerContext for AOT compatibility.
/// </summary>
public static class OutputFormatter
{
    private static readonly HarJsonContext JsonContext = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });

    public static void WriteJson<T>(T value, TextWriter output) where T : class
    {
        var type = typeof(T);
        WriteJsonTyped(value, type, output);
    }

    private static void WriteJsonTyped<T>(T value, Type type, TextWriter output) where T : class
    {
        var json = type switch
        {
            _ when type == typeof(HarSummary) =>
                JsonSerializer.Serialize(value, JsonContext.HarSummary),
            _ when type == typeof(List<HarEntrySummary>) =>
                JsonSerializer.Serialize(value, JsonContext.ListHarEntrySummary),
            _ when type == typeof(List<EntryOutput>) =>
                JsonSerializer.Serialize(value, JsonContext.ListEntryOutput),
            _ when type == typeof(List<DomainStats>) =>
                JsonSerializer.Serialize(value, JsonContext.ListDomainStats),
            _ when type == typeof(ShowOutput) =>
                JsonSerializer.Serialize(value, JsonContext.ShowOutput),
            _ => throw new NotSupportedException($"Type {type.Name} is not registered in HarJsonContext for AOT serialization.")
        };
        output.Write(json);
    }

    public static void WriteSummaryTable(HarSummary summary, TextWriter output)
    {
        output.WriteLine($"Version:      {summary.Version ?? "N/A"}");
        output.WriteLine($"Pages:        {summary.PageCount}");
        output.WriteLine($"Total Entries:{summary.TotalEntries}");
        output.WriteLine($"Request:      {FormatBytes(summary.TotalRequestBytes)}");
        output.WriteLine($"Response:     {FormatBytes(summary.TotalResponseBytes)}");
        output.WriteLine();
        output.WriteLine("Timings (ms):");
        output.WriteLine($"  Min:    {summary.Timings.MinMs:F1}");
        output.WriteLine($"  Max:    {summary.Timings.MaxMs:F1}");
        output.WriteLine($"  Avg:    {summary.Timings.AvgMs:F1}");
        output.WriteLine($"  Median: {summary.Timings.MedianMs:F1}");
        output.WriteLine();
        output.WriteLine("Status Codes:");
        foreach (var kv in summary.StatusCodeDistribution.OrderBy(k => k.Key))
            output.WriteLine($"  {kv.Key}: {kv.Value}");
        output.WriteLine();
        output.WriteLine("Top Domains:");
        foreach (var d in summary.TopDomains)
            output.WriteLine($"  {d}");
        output.WriteLine();
        output.WriteLine("Top Content Types:");
        foreach (var ct in summary.TopContentTypes)
            output.WriteLine($"  {ct}");
    }

    public static void WriteEntryList(List<HarEntrySummary> entries, TextWriter output)
    {
        if (entries.Count == 0)
        {
            output.WriteLine("No entries found.");
            return;
        }

        // Column widths
        var idW = Math.Max(entries.Max(e => e.Id.ToString().Length), 4);
        var methodW = Math.Max(entries.Max(e => e.Method.Length), 6);
        var statusW = 6;
        var timeW = Math.Max(entries.Max(e => e.TimeMs.ToString("F0").Length), 4);
        var sizeW = Math.Max(entries.Max(e => FormatBytes(e.TransferSize).Length), 5);
        var mimeW = Math.Max(entries.Max(e => (e.MimeType ?? "").Length), 8);
        if (mimeW > 25) mimeW = 25;
        var domainW = Math.Max(entries.Max(e => (e.Domain ?? "").Length), 6);
        if (domainW > 30) domainW = 30;

        output.WriteLine($"{"ID".PadRight(idW)}  {"Method".PadRight(methodW)}  {"Status".PadRight(statusW)}  {"Time".PadRight(timeW)}  {"Size".PadRight(sizeW)}  {"MIME".PadRight(mimeW)}  {"Domain".PadRight(domainW)}  URL");
        output.WriteLine(new string('-', idW + methodW + statusW + timeW + sizeW + mimeW + domainW + 8 + 30));

        foreach (var e in entries)
        {
            var mime = (e.MimeType ?? "").Length > 25 ? (e.MimeType ?? "")[..22] + "..." : (e.MimeType ?? "");
            var domain = (e.Domain ?? "").Length > 30 ? (e.Domain ?? "")[..27] + "..." : (e.Domain ?? "");
            var url = e.Url.Length > 80 ? e.Url[..77] + "..." : e.Url;
            output.WriteLine($"{e.Id.ToString().PadRight(idW)}  {e.Method.PadRight(methodW)}  {e.Status.ToString().PadRight(statusW)}  {e.TimeMs.ToString("F0").PadRight(timeW)}  {FormatBytes(e.TransferSize).PadRight(sizeW)}  {mime.PadRight(mimeW)}  {domain.PadRight(domainW)}  {url}");
        }
    }

    public static void WriteDomainList(List<DomainStats> domains, TextWriter output)
    {
        if (domains.Count == 0)
        {
            output.WriteLine("No domains found.");
            return;
        }

        output.WriteLine($"{"Domain".PadRight(40)}  {"Requests",8}  {"Errors",6}  {"Total Resp",10}  {"Avg Time",8}");
        output.WriteLine(new string('-', 80));
        foreach (var d in domains)
        {
            var name = d.Domain.Length > 38 ? d.Domain[..35] + "..." : d.Domain;
            output.WriteLine($"{name.PadRight(40)}  {d.RequestCount,8}  {d.ErrorCount,6}  {FormatBytes(d.TotalResponseBytes),10}  {$"{d.AvgTimeMs:F1}ms",8}");
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B",
    };
}
