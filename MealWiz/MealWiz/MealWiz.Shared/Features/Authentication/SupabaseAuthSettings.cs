namespace MealWiz.Shared.Features.Authentication;

/// <summary>
/// Where GoTrue lives and the anon key, for the two calls the Supabase SDK can't make (it sends no redirect on an
/// email change and drops <c>new_email</c>). Anon key only (SEC-01).
/// </summary>
public record SupabaseAuthSettings(string Url, string AnonKey);
