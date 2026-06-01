---
name: har-analyzer
description: Guidance for using the HarAnalyzer CLI tool to inspect HAR (HTTP Archive) files without loading the full file into context. Use when the user asks about a .har file, wants to analyze network traffic, or needs to inspect HTTP requests/responses.
---

# HarAnalyzer CLI — Agent Usage Guide

You have access to `har-analyzer`, a CLI tool for querying HAR files. HAR files can be 100MB–1GB+, so **never read them directly**. Always use this tool.

## Workflow

Always follow this progression:

```
1. summary   → Get the big picture
2. list      → Filter to what you need
3. show      → Drill into a specific entry
```

### 1. Get an overview

```bash
har-analyzer summary <file.har>
```

Returns JSON with: total entries, status code distribution, timing stats, top domains, content types. Use this to understand what's in the file and decide what to investigate next.

### 2. Filter entries

```bash
har-analyzer list <file.har> [--filter-*] [--limit N] [--format json|table]
```

Available filters:
- `--filter-url <substring>` — URL contains (case-insensitive)
- `--filter-status <code>` — exact HTTP status (e.g., 404, 500)
- `--filter-method <method>` — GET, POST, PUT, DELETE, etc.
- `--filter-domain <substring>` — domain contains (case-insensitive)
- `--filter-mime <substring>` — MIME type contains (case-insensitive, e.g., "json", "html")
- `--min-time <ms>` — only entries with time >= this value
- `--limit <n>` / `-n <n>` — maximum results (default: 50)
- `--format json|table` / `-f json|table` — output format (default: json)

### 3. Inspect an entry

```bash
har-analyzer show <file.har> --id <N> [--format json|table]
```

The ID is the 0-based index from `list` output. Returns the full entry JSON (request, response, headers, timings, etc.).

### Other commands

```bash
har-analyzer errors <file.har> [--filter-*]   # Only 4xx/5xx requests
har-analyzer domains <file.har>                # Domain-level aggregation
```

## Critical Rules

1. **Always use `--limit`** when you don't know the result size. Default is 50, which is safe. Increase only if you have a specific reason.
2. **Start with `summary`** — never jump straight to `list` without knowing the file's scale.
3. **Use JSON format for programmatic consumption** (default). Use `--format table` only when displaying results to the user.
4. **Filter before listing** — use `--filter-*` options to narrow results. Don't list everything and then sift through it mentally.
5. **For full request/response bodies**: `show` returns the complete entry. If the body is large (e.g., base64-encoded images), warn the user before displaying it.
6. **Build the tool first** if it hasn't been compiled: `dotnet build HarAnalyzer/HarAnalyzer.csproj`

## Example Session

```bash
# "What's in this HAR file?"
har-analyzer summary capture.har
# → 5000 entries, 12% errors, mostly api.example.com

# "Show me the failures"
har-analyzer errors capture.har --limit 10
# → 10 error entries with IDs

# "What does entry #42 look like?"
har-analyzer show capture.har --id 42
# → Full request/response JSON

# "Any slow API calls?"
har-analyzer list capture.har --min-time 1000 --filter-mime json --limit 5
```
