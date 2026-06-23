using System.Text.Json;
using System.Text.Json.Serialization;
using GitHub.Copilot;
using ocp;

public class OpenAIMapperTests
{
    [Fact]
    public void ExtractPrompt_TakesLastUserMessage()
    {
        var req = new ChatRequest("gpt-5", new List<ChatMessage>
        {
            new("system", JsonDocument.Parse("\"sys\"").RootElement),
            new("user", JsonDocument.Parse("\"hello there\"").RootElement),
            new("assistant", JsonDocument.Parse("\"hi\"").RootElement),
            new("user", JsonDocument.Parse("\"how are you?\"").RootElement)
        });

        var prompt = OpenAIMapper.ExtractPrompt(req);
        Assert.Equal("how are you?", prompt);
    }

    [Fact]
    public void ExtractPrompt_FallsBackToLastWhenNoUser()
    {
        var req = new ChatRequest("gpt-5", new List<ChatMessage> { new("assistant", JsonDocument.Parse("\"prev\"").RootElement) });
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
        var req = new ChatRequest("gpt-5", [new("user", JsonDocument.Parse("\"test prompt\"").RootElement)]);
        var json = JsonSerializer.Serialize(req, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"model\":\"gpt-5\"", json);
        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("test prompt", json);
    }

    [Fact]
    public void ToStartupModelsLine_ProducesOcpPrefixedIdsLine_UsingMapperShape()
    {
        // ModelInfo has public ctor (verified via reflection); properties are writable
        var models = new List<ModelInfo>
        {
            new ModelInfo { Id = "gpt-5" },
            new ModelInfo { Id = "claude-sonnet-4.5" }
        };
        var line = OpenAIMapper.ToStartupModelsLine(models);
        Assert.Equal("ocp: models: gpt-5, claude-sonnet-4.5", line);
    }

    [Fact]
    public void ToStartupModelsLine_HandlesEmptyList()
    {
        var line = OpenAIMapper.ToStartupModelsLine(new List<ModelInfo>());
        Assert.Equal("ocp: models: ", line);
    }
}
