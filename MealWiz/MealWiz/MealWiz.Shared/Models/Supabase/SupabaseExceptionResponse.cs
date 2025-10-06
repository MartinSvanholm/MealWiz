using System.Net;
using System.Text.Json.Serialization;

namespace MealWiz.Shared.Models.Supabase;

public class SupabaseExceptionResponse
{
    public int code { get; set; }
    public string? error_code { get; set; }
    public string? msg { get; set; }
}
