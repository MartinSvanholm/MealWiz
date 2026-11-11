using System.Text.Json;

namespace MealWiz.Shared.Features.Authentication;

/// <summary>Reads GoTrue's machine-readable <c>error_code</c>; its message text (<c>Reason</c>) can change between versions.</summary>
public static class GotrueErrorCode
{
    public const string WeakPassword = "weak_password";
    public const string SamePassword = "same_password";
    public const string EmailExists = "email_exists";
    public const string ValidationFailed = "validation_failed";

    /// <summary>The <c>error_code</c> of a GoTrue JSON error body, or <c>null</c> for anything else.</summary>
    public static string? Read(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error_code", out var code)
                && code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
