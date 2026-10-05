using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SmartHadithTree.Api;
using Xunit;

namespace SmartHadithTree.Tests.Api;

public class ExtraBodyHandlerTests
{
    private sealed class Recorder : HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static async Task<string?> Send(string extraJson, HttpMethod method, string? body, string mediaType = "application/json")
    {
        var recorder = new Recorder();
        var handler = new ExtraBodyHandler(ExtraBodyHandler.Parse(extraJson)!) { InnerHandler = recorder };
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(method, "http://localhost/v1/chat/completions");
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, mediaType);
        await client.SendAsync(request);
        return recorder.Body;
    }

    [Fact]
    public async Task AddsTheConfiguredFields_AndKeepsTheRequestFields()
    {
        var sent = await Send("{\"reasoning\":{\"enabled\":false}}", HttpMethod.Post, "{\"model\":\"m\",\"messages\":[]}");

        var json = JsonNode.Parse(sent!)!.AsObject();
        json["model"]!.GetValue<string>().Should().Be("m");
        json["reasoning"]!["enabled"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ConfiguredFieldsReplaceRequestFieldsOfTheSameName()
    {
        var sent = await Send("{\"max_tokens\":300}", HttpMethod.Post, "{\"model\":\"m\",\"max_tokens\":4096}");

        JsonNode.Parse(sent!)!["max_tokens"]!.GetValue<int>().Should().Be(300);
    }

    [Fact]
    public async Task LeavesNonJsonAndNonPostRequestsAlone()
    {
        (await Send("{\"a\":1}", HttpMethod.Post, "plain text", "text/plain")).Should().Be("plain text");
        (await Send("{\"a\":1}", HttpMethod.Post, "not json")).Should().Be("not json");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Parse_ReturnsNull_ForEmptyOrNonObjectSettings(string? setting) =>
        ExtraBodyHandler.Parse(setting).Should().BeNull();
}
