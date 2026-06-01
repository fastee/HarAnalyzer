using System.CommandLine;
using System.Text.Json;
using HarAnalyzer.Models;

namespace HarAnalyzer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var fileArg = new Argument<FileInfo>("file") { Description = "Path to the HAR file to analyze" };

        // ── Common options reused across commands ──
        var limitOpt = new Option<int>("--limit", ["-n"])
        {
            Description = "Maximum number of entries to return",
            DefaultValueFactory = _ => 50,
        };

        var formatOpt = new Option<string>("--format", ["-f"])
        {
            Description = "Output format: json or table",
            DefaultValueFactory = _ => "json",
        };

        var filterUrlOpt = new Option<string?>("--filter-url")
        {
            Description = "Filter entries whose URL contains this string (case-insensitive)",
        };
        var filterStatusOpt = new Option<int?>("--filter-status")
        {
            Description = "Filter entries by HTTP status code (e.g., 404)",
        };
        var filterMethodOpt = new Option<string?>("--filter-method")
        {
            Description = "Filter entries by HTTP method (GET, POST, etc.)",
        };
        var filterDomainOpt = new Option<string?>("--filter-domain")
        {
            Description = "Filter entries by domain (case-insensitive contains)",
        };
        var filterMimeOpt = new Option<string?>("--filter-mime")
        {
            Description = "Filter entries by MIME type (case-insensitive contains, e.g., 'json', 'html')",
        };
        var minTimeOpt = new Option<double?>("--min-time")
        {
            Description = "Filter entries with time >= this value (ms)",
        };

        var idOpt = new Option<int>("--id", ["-i"])
        {
            Description = "Entry ID (0-based index) to show",
            DefaultValueFactory = _ => -1,
        };

        // ── Commands ──

        var summaryCmd = new Command("summary", "Show a high-level overview of the HAR file")
        {
            fileArg, formatOpt,
        };
        summaryCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = pr.GetValue(fileArg)!;
            var format = pr.GetValue(formatOpt)!;
            await RunSummary(file.FullName, format);
        });

        var listCmd = new Command("list", "List entries with optional filtering")
        {
            fileArg, limitOpt, formatOpt, filterUrlOpt, filterStatusOpt,
            filterMethodOpt, filterDomainOpt, filterMimeOpt, minTimeOpt,
        };
        listCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await RunList(
                pr.GetValue(fileArg)!.FullName,
                pr.GetValue(limitOpt),
                pr.GetValue(formatOpt)!,
                pr.GetValue(filterUrlOpt),
                pr.GetValue(filterStatusOpt),
                pr.GetValue(filterMethodOpt),
                pr.GetValue(filterDomainOpt),
                pr.GetValue(filterMimeOpt),
                pr.GetValue(minTimeOpt));
        });

        var showCmd = new Command("show", "Show full details of an entry by ID")
        {
            fileArg, formatOpt, idOpt,
        };
        showCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await RunShow(pr.GetValue(fileArg)!.FullName, pr.GetValue(idOpt), pr.GetValue(formatOpt)!);
        });

        var errorsCmd = new Command("errors", "Show only failed requests (4xx and 5xx)")
        {
            fileArg, limitOpt, formatOpt, filterUrlOpt, filterDomainOpt, minTimeOpt,
        };
        errorsCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await RunErrors(
                pr.GetValue(fileArg)!.FullName,
                pr.GetValue(limitOpt),
                pr.GetValue(formatOpt)!,
                pr.GetValue(filterUrlOpt),
                pr.GetValue(filterDomainOpt),
                pr.GetValue(minTimeOpt));
        });

        var domainsCmd = new Command("domains", "Aggregated domain analysis")
        {
            fileArg, limitOpt, formatOpt,
        };
        domainsCmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            await RunDomains(pr.GetValue(fileArg)!.FullName, pr.GetValue(limitOpt), pr.GetValue(formatOpt)!);
        });

        var rootCmd = new RootCommand("HarAnalyzer — CLI tool for analyzing HAR files without loading full context")
        {
            summaryCmd, listCmd, showCmd, errorsCmd, domainsCmd,
        };

        var parseResult = rootCmd.Parse(args);
        return await parseResult.InvokeAsync();
    }

    // ── Command implementations ──

    static async Task RunSummary(string filePath, string format)
    {
        try
        {
            var summary = await HarReader.ComputeSummaryAsync(filePath);
            if (format == "table")
                OutputFormatter.WriteSummaryTable(summary, Console.Out);
            else
                OutputFormatter.WriteJson(summary, Console.Out);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    static async Task RunList(string filePath, int limit, string format,
        string? filterUrl, int? filterStatus, string? filterMethod,
        string? filterDomain, string? filterMime, double? minTime)
    {
        try
        {
            var results = new List<HarEntrySummary>();
            await foreach (var (entry, index) in HarReader.StreamEntriesAsync(filePath))
            {
                if (results.Count >= limit) break;

                var summary = HarReader.ParseEntrySummary(entry, index);

                if (filterUrl != null && !summary.Url.Contains(filterUrl, StringComparison.OrdinalIgnoreCase)) continue;
                if (filterStatus.HasValue && summary.Status != filterStatus.Value) continue;
                if (filterMethod != null && !summary.Method.Equals(filterMethod, StringComparison.OrdinalIgnoreCase)) continue;
                if (filterDomain != null && (summary.Domain == null || !summary.Domain.Contains(filterDomain, StringComparison.OrdinalIgnoreCase))) continue;
                if (filterMime != null && (summary.MimeType == null || !summary.MimeType.Contains(filterMime, StringComparison.OrdinalIgnoreCase))) continue;
                if (minTime.HasValue && summary.TimeMs < minTime.Value) continue;

                results.Add(summary);
            }

            if (format == "table")
                OutputFormatter.WriteEntryList(results, Console.Out);
            else
                OutputFormatter.WriteJson(results.Select(EntryOutput.From).ToList(), Console.Out);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    static async Task RunShow(string filePath, int id, string format)
    {
        try
        {
            if (id < 0) { Console.Error.WriteLine("Error: --id is required. Use 'list' to find entry IDs."); return; }

            // Load file directly — keep the JsonDocument alive until we're done outputting
            byte[] data;
            await using (var stream = File.OpenRead(filePath))
            {
                data = new byte[stream.Length];
                var bytesRead = 0;
                while (bytesRead < data.Length)
                {
                    var n = await stream.ReadAsync(data.AsMemory(bytesRead));
                    if (n == 0) break;
                    bytesRead += n;
                }
            }

            using var doc = JsonDocument.Parse(data);
            if (!doc.RootElement.TryGetProperty("log", out var log)) { Console.Error.WriteLine("Invalid HAR: missing 'log'."); return; }
            if (!log.TryGetProperty("entries", out var entries)) { Console.Error.WriteLine("Invalid HAR: missing 'entries'."); return; }

            var index = 0;
            JsonElement? found = null;
            foreach (var entry in entries.EnumerateArray())
            {
                if (index == id) { found = entry; break; }
                index++;
            }

            if (found == null) { Console.Error.WriteLine($"Entry with ID {id} not found."); return; }

            if (format == "table")
            {
                // Use WriteTo for AOT-safe pretty-printing of JsonElement
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                found.Value.WriteTo(writer);
                writer.Flush();
                Console.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
            else
            {
                OutputFormatter.WriteJson(new ShowOutput { Id = id, Entry = found.Value }, Console.Out);
            }
        }
        catch (Exception ex) { WriteError(ex); }
    }

    static async Task RunErrors(string filePath, int limit, string format,
        string? filterUrl, string? filterDomain, double? minTime)
    {
        try
        {
            var results = new List<HarEntrySummary>();
            await foreach (var (entry, index) in HarReader.StreamEntriesAsync(filePath))
            {
                var summary = HarReader.ParseEntrySummary(entry, index);
                if (!summary.IsError) continue;

                if (filterUrl != null && !summary.Url.Contains(filterUrl, StringComparison.OrdinalIgnoreCase)) continue;
                if (filterDomain != null && (summary.Domain == null || !summary.Domain.Contains(filterDomain, StringComparison.OrdinalIgnoreCase))) continue;
                if (minTime.HasValue && summary.TimeMs < minTime.Value) continue;

                results.Add(summary);
                if (results.Count >= limit) break;
            }

            if (format == "table")
                OutputFormatter.WriteEntryList(results, Console.Out);
            else
                OutputFormatter.WriteJson(results.Select(EntryOutput.From).ToList(), Console.Out);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    static async Task RunDomains(string filePath, int limit, string format)
    {
        try
        {
            var domainMap = new Dictionary<string, DomainStats>(StringComparer.OrdinalIgnoreCase);
            await foreach (var (entry, index) in HarReader.StreamEntriesAsync(filePath))
            {
                var summary = HarReader.ParseEntrySummary(entry, index);
                var domain = summary.Domain ?? "(unknown)";

                if (!domainMap.TryGetValue(domain, out var stats))
                {
                    stats = new DomainStats { Domain = domain };
                    domainMap[domain] = stats;
                }

                domainMap[domain] = stats with
                {
                    RequestCount = stats.RequestCount + 1,
                    TotalResponseBytes = stats.TotalResponseBytes + summary.ResponseBodySize,
                    ErrorCount = stats.ErrorCount + (summary.IsError ? 1 : 0),
                    AvgTimeMs = ((stats.AvgTimeMs * stats.RequestCount) + summary.TimeMs) / (stats.RequestCount + 1),
                };
            }

            var sorted = domainMap.Values.OrderByDescending(d => d.RequestCount).Take(limit).ToList();

            if (format == "table")
                OutputFormatter.WriteDomainList(sorted, Console.Out);
            else
                OutputFormatter.WriteJson(sorted, Console.Out);
        }
        catch (Exception ex) { WriteError(ex); }
    }

    static void WriteError(Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.ExitCode = 1;
    }
}
