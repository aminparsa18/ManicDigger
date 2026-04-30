
/// <summary>
/// A simple string-keyed settings store. All values are persisted as strings
/// and converted on read. Backed by a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public class Preferences
{
    private readonly Dictionary<string, string> items = new();

    // -------------------------------------------------------------------------
    // String
    // -------------------------------------------------------------------------

    public string GetString(string key, string default_) =>
        items.TryGetValue(key, out string value) ? value : default_;

    public void SetString(string key, string value) =>
        items[key] = value;

    // -------------------------------------------------------------------------
    // Bool (stored as "0" / "1")
    // -------------------------------------------------------------------------

    public bool GetBool(string key, bool default_)
    {
        string value = GetString(key, null);
        return value switch
        {
            "0" => false,
            "1" => true,
            _ => default_
        };
    }

    public void SetBool(string key, bool value) =>
        SetString(key, value ? "1" : "0");

    // -------------------------------------------------------------------------
    // Int (stored as string, parsed via float to handle decimals gracefully)
    // -------------------------------------------------------------------------

    public int GetInt(string key, int default_)
    {
        string raw = GetString(key, null);
        if (raw == null) return default_;
        return float.TryParse(raw, out float result) ? (int)result : default_;
    }

    public void SetInt(string key, int value) =>
        SetString(key, value.ToString());

    public IEnumerable<string> ToLines() =>
        items.Select(kvp => $"{kvp.Key}={kvp.Value}");

    // -------------------------------------------------------------------------
    // Collection
    // -------------------------------------------------------------------------

    public int GetKeysCount() => items.Count;

    public string GetKey(int i) => items.Keys.ElementAtOrDefault(i);

    internal void Remove(string key) => items.Remove(key);
}

