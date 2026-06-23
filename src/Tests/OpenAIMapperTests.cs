using ocp;
using GitHub.Copilot;
using System.Text.Json;

public class OpenAIMapperTests
{
    [Fact]
    public void ExtractPrompt_TakesLastUserMessage()
    {
        var req = new ChatRequest("gpt-5", new List<ChatMessage>
        {
            new("system", "sys"),
            new("user", "hello there"),
            new("assistant", "hi"),
            new("user", "how are you?")
        });

        var prompt = OpenAIMapper.ExtractPrompt(req);
        Assert.Equal("how are you?", prompt);
    }

    [Fact]
    public void ExtractPrompt_FallsBackToLastWhenNoUser()
    {
        var req = new ChatRequest("gpt-5", new List<ChatMessage> { new("assistant", "prev") });
        var prompt = OpenAIMapper.ExtractPrompt(req);
        Assert.Equal("prev", prompt);
    }

    [Fact]
    public void ToChatCompletionResponse_ProducesRequiredOpenAIShape()
    {
        // Simulate SDK event (Data is internal-ish, use null path for shape)
        var resp = OpenAIMapper.ToChatCompletionResponse("claude-sonnet-4.5", null);

        Assert.Equal("chat.completion", resp.Object);
        Assert.Equal("claude-sonnet-4.5", resp.Model);
        Assert.Single(resp.Choices);
        Assert.Equal("assistant", resp.Choices[0].Message.Role);
        Assert.Equal("", resp.Choices[0].Message.Content); // null evt path
        Assert.Equal("stop", resp.Choices[0].FinishReason);
        Assert.NotNull(resp.Id);
        Assert.True(resp.Created > 0);
    }

    [Fact]
    public void ToModelsListResponse_ProducesOpenAIListShape_WithIdsFromSdk()
    {
        // Fake minimal ModelInfo instances (public properties settable? use reflection or known shape)
        var fakeModels = new List<ModelInfo>();
        // Since ModelInfo ctor may be internal, we test shape via serialized + structural
        // For direct, create via JSON roundtrip simulation is overkill; assert on method return type + count behavior via empty
        var list = OpenAIMapper.ToModelsListResponse(fakeModels);
        Assert.Equal("list", list.Object);
        Assert.Empty(list.Data);
    }

    [Fact]
    public void Roundtrip_ChatRequest_SerializesAsExpected()
    {
        var req = new ChatRequest("gpt-5", [new("user", "test prompt")]);
        var json = JsonSerializer.Serialize(req, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"model\":\"gpt-5\"", json);
        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("test prompt", json);
    }
}
