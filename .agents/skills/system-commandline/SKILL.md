---
name: system-commandline
description: Guidance for using System.CommandLine 2.0.8 in this .NET project. Use when adding CLI commands, options, arguments, or fixing System.CommandLine compilation errors.
---

# System.CommandLine 2.0.8 — API Reference

This project uses **`System.CommandLine` version 2.0.8** (NuGet). This is the newer redesigned API (NOT the old `System.CommandLine.Hosting`/`CommandHandler.Create` pattern).

## Quick Reference

```csharp
using System.CommandLine;  // Everything is in this namespace
```

### Namespace

All public types live in **`System.CommandLine`**. That's the only namespace needed for basic use. Other namespaces like `System.CommandLine.Invocation` are NOT needed for the patterns below.

---

## Constructors

### `Argument<T>`

```csharp
// One-param constructor (name). Description is a property.
var fileArg = new Argument<FileInfo>("file") { Description = "Path to the file" };
```

| Signature | Works? |
|-----------|--------|
| `new Argument<FileInfo>("file")` | ✅ |
| `new Argument<FileInfo>("file", "desc")` | ❌ No 2-param ctor |

### `Option<T>`

```csharp
// Two-param ctor: (name, string[] aliases). Description and default are properties.
var limitOpt = new Option<int>("--limit", ["-n"])
{
    Description = "Maximum entries",
    DefaultValueFactory = _ => 50,  // NOT () => 50 — delegate takes ArgumentResult
};

var verboseOpt = new Option<bool>("--verbose", ["-v"])
{
    Description = "Enable verbose output",
};
```

| Signature | Works? |
|-----------|--------|
| `new Option<int>("--limit", ["-n"])` | ✅ |
| `new Option<int>("--limit", () => 50, "desc")` | ❌ |
| `.AddAlias("-n")` | ❌ No such method — use aliases array in ctor |

### `DefaultValueFactory` delegate

The delegate signature is **`Func<ArgumentResult, T>`**, NOT `Func<T>`:

```csharp
DefaultValueFactory = _ => 50,          // ✅
DefaultValueFactory = () => 50,         // ❌ CS1593: 未采用 0 个参数
DefaultValueFactory = arg => arg.Tokens.Any() ? 1 : 50,  // ✅ can inspect arg
```

### `Command`

```csharp
// Two-param ctor
var cmd = new Command("name", "description");
```

### `RootCommand`

```csharp
// One-param ctor
var root = new RootCommand("app description");
```

---

## Setting Handlers

Use **`command.SetAction(...)`**, NOT `command.Handler = ...` and NOT `CommandHandler.Create(...)`.

### Available overloads

```csharp
// Sync, returns void
cmd.SetAction((ParseResult pr) => { ... });

// Sync, returns int (exit code)
cmd.SetAction((ParseResult pr) => { return 0; });

// Async, returns Task
cmd.SetAction(async (ParseResult pr) => { await ...; });

// Async, returns Task<int> (exit code)
cmd.SetAction(async (ParseResult pr) => { return 0; });

// Async with CancellationToken — PREFERRED for I/O-bound work
cmd.SetAction(async (ParseResult pr, CancellationToken ct) => { await ...; });
```

### Pattern: binding option values

Inside the handler, use `pr.GetValue(option)` to extract parsed values:

```csharp
var fileArg = new Argument<FileInfo>("file") { ... };
var limitOpt = new Option<int>("--limit", ["-n"]) { DefaultValueFactory = _ => 50 };

var cmd = new Command("list", "List entries");
cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var file = pr.GetValue(fileArg)!;     // Argument
    var limit = pr.GetValue(limitOpt);    // Option
    await DoWork(file.FullName, limit);
});
```

---

## Invocation

```csharp
var rootCmd = new RootCommand("description") { cmd1, cmd2, ... };

// Parse + invoke in two steps:
var parseResult = rootCmd.Parse(args);
return await parseResult.InvokeAsync();

// NOT: rootCmd.InvokeAsync(args)  — this method doesn't exist on RootCommand
```

---

## Common Pitfalls

### ❌ `CliCommand` / `CliOption` / `CliArgument` don't exist

These are from an unreleased preview. In 2.0.8, use `Command`, `Option<T>`, `Argument<T>`.

### ❌ `AddAlias()` doesn't exist

```csharp
// ❌
new Option<int>("--limit").AddAlias("-n");

// ✅
new Option<int>("--limit", ["-n"]);
```

### ❌ `SetHandler` doesn't exist

Use `SetAction` instead.

### ❌ `InvokeAsync(args)` on RootCommand doesn't exist

Use `rootCmd.Parse(args).InvokeAsync()`.

### ❌ `DefaultValueFactory` is `Func<ArgumentResult, T>`, not `Func<T>`

Always `_ => value`, never `() => value`.

---

## Full Working Example

```csharp
using System.CommandLine;

var fileArg = new Argument<FileInfo>("input") { Description = "Input file" };

var countOpt = new Option<int>("--count", ["-c"])
{
    Description = "Number of items",
    DefaultValueFactory = _ => 10,
};

var cmd = new Command("process", "Process the file")
{
    fileArg,
    countOpt,
};

cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    var file = pr.GetValue(fileArg)!;
    var count = pr.GetValue(countOpt);
    await ProcessAsync(file.FullName, count, ct);
});

var root = new RootCommand("My CLI")
{
    cmd,
};

var parseResult = root.Parse(args);
return await parseResult.InvokeAsync();
```
