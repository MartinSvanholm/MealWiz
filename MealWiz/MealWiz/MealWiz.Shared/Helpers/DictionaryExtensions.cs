namespace MealWiz.Shared.Helpers;

public static class DictionaryExtensions
{
    public static void Add<Tkey, TValue>(this Dictionary<Tkey, List<TValue>> keyValuePairs, Tkey key, TValue value)
    {
        if (keyValuePairs.TryGetValue(key, out var list))
        {
            list.Add(value);
        } else
        {
            keyValuePairs[key] = [value];
        }
    }
}