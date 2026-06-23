using GitHub.Copilot;
using System.Text.Json.Serialization;

namespace ocp;

public static class OpenAIMapper
{
    public static string ExtractPrompt(ChatRequest req)
    {
        if (req.Messages is null || req.Messages.Count == 0) return "";
        var lastUser = req.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        return lastUser?.Content ?? req.Messages.LastOrDefault()?.Content ?? "";
    }

    public static ChatCompletionResponse ToChatCompletionResponse(string model, AssistantMessageEvent? evt)
    {
        var content = evt?.Data?.Content ?? "";
        return new ChatCompletionResponse(
            "chatcmpl-" + Guid.NewGuid().ToString("N"),
            "chat.completion",
            (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model,
            [ new Choice(0, new ResponseMessage("assistant", content), "stop") ],
            new Usage(0, 0, 0)
        );
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
public record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string? Content);

public record ChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<ChatMessage>? Messages,
    [property: JsonPropertyName("stream")] bool? Stream = null);

public record ResponseMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string? Content);

public record Choice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")] ResponseMessage Message,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

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
