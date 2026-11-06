namespace MealWiz.Shared.Localization;

/// <summary>
/// Marker type for the shared resource (<c>SharedResource.resx</c>, English, and <c>SharedResource.da.resx</c>,
/// Danish, beside this file): common words, shared messages and the app shell's texts. Inject
/// <c>IStringLocalizer&lt;SharedResource&gt;</c>; code without DI uses <see cref="SharedTexts"/>. Feature texts go in
/// the feature's own resource, never here (ARCHITECTURE.md, "Localization").
/// </summary>
public sealed class SharedResource;
