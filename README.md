# HarAnalyzer

A fast, memory-efficient CLI tool for analyzing HAR (HTTP Archive) files. Built with .NET Native AOT for instant startup and minimal footprint.

## Features

- **Streaming parser** – reads entries one at a time without loading the full file into managed objects
- **Multiple commands** – summary, list, show, errors, domains
- **Rich filtering** – filter by URL, status code, method, domain, MIME type, and response time
- **Dual output** – JSON (default) or table format
- **Native AOT** – single-file executable, no runtime required

## Installation

### Download a pre-built binary

Download the latest release from the [Releases page](https://github.com/fastee/HarAnalyzer/releases).

### Build from source

```bash
git clone https://github.com/fastee/HarAnalyzer.git
cd HarAnalyzer
dotnet publish -c Release
```

The executable will be at `HarAnalyzer/bin/Release/net10.0/publish/HarAnalyzer`.

## Usage

```bash
HarAnalyzer <file> <command> [options]
```

### Commands

| Command | Description |
|---------|-------------|
| `summary` | Show a high-level overview of the HAR file |
| `list` | List entries with optional filtering |
| `show` | Show full details of an entry by ID |
| `errors` | Show only failed requests (4xx and 5xx) |
| `domains` | Aggregated domain analysis |

### Options

| Option | Short | Description | Applies to |
|--------|-------|-------------|------------|
| `--limit` | `-n` | Max entries to return (default: 50) | list, errors, domains |
| `--format` | `-f` | Output format: `json` or `table` | summary, list, show, errors, domains |
| `--filter-url` | | Filter entries whose URL contains this string | list, errors |
| `--filter-status` | | Filter by HTTP status code | list |
| `--filter-method` | | Filter by HTTP method (GET, POST, etc.) | list |
| `--filter-domain` | | Filter by domain | list, errors |
| `--filter-mime` | | Filter by MIME type | list |
| `--min-time` | | Filter entries with time >= value (ms) | list, errors |
| `--id` | `-i` | Entry ID (0-based index) to show | show |

### Examples

```bash
# Show summary
HarAnalyzer trace.har summary

# List entries in table format
HarAnalyzer trace.har list --format table --limit 20

# Filter failed API calls
HarAnalyzer trace.har errors --filter-url /api/ --min-time 1000

# Show full details of an entry
HarAnalyzer trace.har show --id 42

# Domain breakdown
HarAnalyzer trace.har domains --limit 10 --format table
```

## License

MIT
