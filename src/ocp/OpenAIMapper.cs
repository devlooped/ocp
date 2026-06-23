using System.Text.Json;
using System.Text.Json.Serialization;
using GitHub.Copilot;

namespace ocp;

public static class OpenAIMapper
{
    public static string ExtractPrompt(ChatRequest req)
    {
        if (req.Messages is null || req.Messages.Count == 0) return "";
        var lastUser = req.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        return lastUser?.GetTextContent() ?? req.Messages.LastOrDefault()?.GetTextContent() ?? "";
    }

    public static (string Content, List<ToolCall>? ToolCalls, string FinishReason) MapAssistantMessage(AssistantMessageEvent? evt)
    {
        var content = evt?.Data?.Content ?? "";
        var toolCalls = MapToolCalls(evt?.Data?.ToolRequests);
        var finishReason = toolCalls is { Count: > 0 } && string.IsNullOrEmpty(content) ? "tool_calls" : "stop";
        return (content, toolCalls, finishReason);
    }

    public static ChatCompletionResponse ToChatCompletionResponse(string model, AssistantMessageEvent? evt)
    {
        var id = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (content, toolCalls, finishReason) = MapAssistantMessage(evt);
        return new ChatCompletionResponse(
            id,
            "chat.completion",
            created,
            model,
            [new Choice(0, new ResponseMessage("assistant", content, toolCalls), finishReason)],
            new Usage(0, 0, 0)
        );
    }

    public static IEnumerable<ChatCompletionChunk> ToStreamChunks(string model, AssistantMessageEvent? evt)
    {
        var id = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (content, toolCalls, finishReason) = MapAssistantMessage(evt);

        yield return new ChatCompletionChunk(id, "chat.completion.chunk", created, model,
            [new StreamChoice(0, new StreamDelta(Role: "assistant"), null)]);

        if (!string.IsNullOrEmpty(content))
        {
            yield return new ChatCompletionChunk(id, "chat.completion.chunk", created, model,
                [new StreamChoice(0, new StreamDelta(Content: content), null)]);
        }

        if (toolCalls is { Count: > 0 })
        {
            for (var i = 0; i < toolCalls.Count; i++)
            {
                var call = toolCalls[i];
                yield return new ChatCompletionChunk(id, "chat.completion.chunk", created, model,
                    [new StreamChoice(0, new StreamDelta(ToolCalls:
                    [
                        new StreamToolCall(i, call.Id, call.Type,
                            new StreamFunction(call.Function.Name, i == 0 ? call.Function.Arguments : ""))
                    ]), null)]);
            }
        }

        yield return new ChatCompletionChunk(id, "chat.completion.chunk", created, model,
            [new StreamChoice(0, new StreamDelta(), finishReason)]);
    }

    static List<ToolCall>? MapToolCalls(AssistantMessageToolRequest[]? requests)
    {
        if (requests is null || requests.Length == 0) return null;

        var calls = new List<ToolCall>();
        foreach (var request in requests)
        {
            var args = request.Arguments is null ? "{}" : JsonSerializer.Serialize(request.Arguments);
            calls.Add(new ToolCall(
                request.ToolCallId ?? "call_" + Guid.NewGuid().ToString("N"),
                "function",
                new FunctionCall(request.Name ?? "unknown", args)));
        }
        return calls;
    }

    public static ModelsListResponse ToModelsListResponse(IList<ModelInfo> models)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = models.Select(m => new ModelObject(m.Id, "model", now, "copilot")).ToList();
        return new ModelsListResponse("list", data);
    }

    /// <summary>
    /// Pure formatter for startup console output. Returns a single "ocp: models: ..." line.
    /// Reuses ToModelsListResponse shape internally for consistency.
    /// </summary>
    public static string ToStartupModelsLine(IList<ModelInfo> models)
    {
        var resp = ToModelsListResponse(models);
        var ids = resp.Data.Select(d => d.Id);
        return "ocp: models: " + string.Join(", ", ids);
    }
}

// OpenAI compatible request/response shapes (minimal, for /v1 compat)
public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] JsonElement? Content)
{
    public string? GetTextContent()
    {
        if (Content is null) return null;
        return Content.Value.ValueKind switch
        {
            JsonValueKind.String => Content.Value.GetString(),
            JsonValueKind.Array => string.Concat(
                Content.Value.EnumerateArray()
                    .Where(part => part.TryGetProperty("text", out _))
                    .Select(part => part.GetProperty("text").GetString())),
            _ => Content.Value.ToString()
        };
    }
}

public record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage>? Messages,
    [property: JsonPropertyName("stream")] bool? Stream = null);

public record ResponseMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("tool_calls")] List<ToolCall>? ToolCalls = null);

public record ToolCall(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] FunctionCall Function);

public record FunctionCall(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments);

public record Choice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")] ResponseMessage Message,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

public record StreamDelta(
    [property: JsonPropertyName("role")] string? Role = null,
    [property: JsonPropertyName("content")] string? Content = null,
    [property: JsonPropertyName("tool_calls")] List<StreamToolCall>? ToolCalls = null);

public record StreamToolCall(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("function")] StreamFunction? Function = null);

public record StreamFunction(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("arguments")] string? Arguments = null);

public record StreamChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("delta")] StreamDelta Delta,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

public record ChatCompletionChunk(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] int Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<StreamChoice> Choices);

public record Usage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens);

public record ChatCompletionResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] int Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<Choice> Choices,
    [property: JsonPropertyName("usage")] Usage? Usage = null);

public record ModelObject(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] int Created,
    [property: JsonPropertyName("owned_by")] string OwnedBy);

public record ModelsListResponse(
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("data")] List<ModelObject> Data);
