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

    public static KeyValuePair<Tkey, TValue>? GetFirstAndRemove<Tkey, TValue>(this Dictionary<Tkey, TValue> keyValuePairs)
    {
        if (keyValuePairs.Count == 0) return null;

        Tkey firstKey = keyValuePairs.Keys.First();
        TValue firstValue = keyValuePairs.Values.First();

        keyValuePairs.Remove(firstKey);

        return new KeyValuePair<Tkey, TValue>(firstKey, firstValue);
    }
}