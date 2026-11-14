using System.Net;
using System.Text;
using System.Text.Json;

namespace MealWiz.Functions.Tests.TestInfrastructure;

/// <summary>
/// Drives the real Anthropic SDK client offline: records every request body and answers with a canned message or
/// error. No test calls the real AI service.
/// </summary>
public sealed class FakeAnthropicHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public List<JsonDocument> Requests { get; } = [];

    public FakeAnthropicHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public FakeAnthropicHandler(HttpResponseMessage response) : this((_, _) => Task.FromResult(response)) { }

    public static FakeAnthropicHandler Answering(string readingJson, string stopReason = "end_turn") =>
        new(Json(HttpStatusCode.OK, Message(readingJson, stopReason)));

    public static FakeAnthropicHandler Failing(HttpStatusCode status, string errorType, string message) =>
        new(Json(status, JsonSerializer.Serialize(new { type = "error", error = new { type = errorType, message } })));

    public static string Message(string readingJson, string stopReason = "end_turn") =>
        JsonSerializer.Serialize(new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model = "claude-haiku-5-5",
            content = new[] { new { type = "text", text = readingJson } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = 1200, output_tokens = 300 }
        });

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) Requests.Add(JsonDocument.Parse(body));
        return await _respond(request, cancellationToken);
    }
}
