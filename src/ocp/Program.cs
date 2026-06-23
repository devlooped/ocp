using GitHub.Copilot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ocp; // for OpenAIMapper + DTOs (pure testable)

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("ocp - OpenAI-compatible self-hosted endpoint using GitHub Copilot SDK");
    Console.WriteLine();
    Console.WriteLine("Usage: ocp [--cwd <path>]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --cwd <path>   Working directory (defaults to ~/.ocp)");
    Console.WriteLine("                 Also used as BaseDirectory for Copilot SDK (COPILOT_HOME).");
    Console.WriteLine();
    Console.WriteLine("Endpoints (http://localhost:<port>):");
    Console.WriteLine("  GET  /v1/models");
    Console.WriteLine("  POST /v1/chat/completions");
    Environment.Exit(0);
}

string? cwd = null;
for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--cwd")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --cwd requires a value (e.g. --cwd /path or --cwd=/path).");
            Environment.Exit(1);
        }
        var val = args[++i];
        if (val.StartsWith("-"))
        {
            Console.Error.WriteLine($"Error: --cwd value looks like a flag: {val}");
            Environment.Exit(1);
        }
        cwd = Path.GetFullPath(val);
        break;
    }
    else if (a.StartsWith("--cwd="))
    {
        var val = a.Substring("--cwd=".Length);
        if (string.IsNullOrWhiteSpace(val) || val.StartsWith("-"))
        {
            Console.Error.WriteLine($"Error: invalid --cwd value in {a}");
            Environment.Exit(1);
        }
        cwd = Path.GetFullPath(val);
        break;
    }
}
if (cwd is null)
{
    cwd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ocp");
}

Directory.CreateDirectory(cwd);
Directory.SetCurrentDirectory(cwd);

Console.WriteLine($"ocp: using working directory: {cwd}");

await using var client = new CopilotClient(new CopilotClientOptions
{
    WorkingDirectory = cwd,
    BaseDirectory = cwd,
    // Use default (ApproveAll is per-session)
});

try
{
    await client.StartAsync();
    Console.WriteLine("ocp: Copilot client started.");
    var models = await client.ListModelsAsync();
    Console.WriteLine(OpenAIMapper.ToStartupModelsLine(models));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ocp: Failed to start Copilot client: {ex.Message}");
    Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Robust port selection (internal, no --port flag per non-goals)
static int FindAvailablePort(int preferred, int maxTries = 5)
{
    for (int p = preferred; p < preferred + maxTries; p++)
    {
        try
        {
            using var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, p);
            l.Start();
            l.Stop();
            return p;
        }
        catch { /* try next */ }
    }
    return preferred; // will fail with clear error below
}

var preferredPort = 11434;
var port = FindAvailablePort(preferredPort);
var listenUrl = $"http://localhost:{port}";

var app = builder.Build();
app.Urls.Clear();
app.Urls.Add(listenUrl);

// Ensure client is always stopped/disposed, even on bind failure or kill
app.Lifetime.ApplicationStopping.Register(() =>
{
    try { client.StopAsync().GetAwaiter().GetResult(); } catch { }
});

app.MapGet("/v1/models", async (HttpContext ctx) =>
{
    try
    {
        var models = await client.ListModelsAsync();
        var resp = OpenAIMapper.ToModelsListResponse(models);
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(resp, jsonOptions));
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = new { message = ex.Message } });
    }
});

app.MapPost("/v1/chat/completions", async (HttpContext ctx) =>
{
    ChatRequest? req;
    try
    {
        req = await ctx.Request.ReadFromJsonAsync<ChatRequest>(jsonOptions);
    }
    catch
    {
        req = null;
    }

    if (req is null || string.IsNullOrWhiteSpace(req.Model) || req.Messages is null || req.Messages.Count == 0)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { error = new { message = "Invalid request: model and messages[] required" } }, jsonOptions);
        return;
    }

    var prompt = OpenAIMapper.ExtractPrompt(req);

    try
    {
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = req.Model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        });

        var assistantEvent = await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });

        var completion = OpenAIMapper.ToChatCompletionResponse(req.Model, assistantEvent);

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(completion, jsonOptions));
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = new { message = ex.Message } }, jsonOptions);
    }
});

app.MapGet("/", () => Results.Ok(new { name = "ocp", status = "ready", endpoints = new[] { "/v1/models", "/v1/chat/completions" } }));

Console.WriteLine($"ocp: listening on {listenUrl} (OpenAI compatible)");
Console.WriteLine("ocp: GET /v1/models , POST /v1/chat/completions");

try
{
    await app.RunAsync();
}
finally
{
    try { await client.StopAsync(); } catch { }
}

// DTOs + pure mappers in OpenAIMapper.cs (public, unit testable from Tests)
