using System.Text;
using System.Text.Json.Nodes;

namespace SmartHadithTree.Api;

/// <summary>
/// Adds provider-specific fields to the JSON body of the requests sent to the chat model, for options the
/// Semantic Kernel connector does not expose (for example a switch that turns the model's "thinking" off).
/// Configured with <c>Together:ExtraBody</c> (a JSON object, environment variable <c>Together__ExtraBody</c>).
/// With nothing configured it is not installed. Fields given here replace fields of the same name in the request.
/// </summary>
public sealed class ExtraBodyHandler(JsonObject extra) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.Content?.Headers.ContentType?.MediaType == "application/json")
        {
            var text = await request.Content.ReadAsStringAsync(cancellationToken);
            JsonObject? body;
            try { body = JsonNode.Parse(text) as JsonObject; }
            catch (System.Text.Json.JsonException) { body = null; }

            if (body != null)
            {
                foreach (var (key, value) in extra) body[key] = value?.DeepClone();
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>The configured JSON object, or null when the setting is empty or is not a JSON object.</summary>
    public static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
