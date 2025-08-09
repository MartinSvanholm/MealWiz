using System.Text.Json.Serialization;

namespace MealWiz.Shared.Models.Supabase;

public class SupabaseExceptionMessage
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("details")]
    public string Details { get; set; }

    [JsonPropertyName("hint")]
    public string Hint { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}