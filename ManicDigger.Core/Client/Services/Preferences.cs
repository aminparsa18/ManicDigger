
/// <summary>
/// A simple string-keyed settings store. All values are persisted as strings
/// and converted on read. Backed by a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public class Preferences : IPreferences
{
    private Dictionary<string, string> items = [];

    public Preferences()
    {
        items = [];
        if (File.Exists(GetPreferencesFilePath()))
        {
            string[] lines = File.ReadAllLines(GetPreferencesFilePath());
            foreach (string l in lines)
            {
                int a = l.IndexOf("=", StringComparison.InvariantCultureIgnoreCase);
                string name = l[..a];
                string value = l[(a + 1)..];
                SetString(name, value);
            }
        }
    }

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

    public void SetValues()
    {
        File.WriteAllLines(GetPreferencesFilePath(), ToLines());
    }

    // -------------------------------------------------------------------------
    // Collection
    // -------------------------------------------------------------------------

    public void Remove(string key) => items.Remove(key);

    private static string GetPreferencesFilePath()
    {
        string path = GameStorePath.GetStorePath();
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return Path.Combine(path, "Preferences.txt");
    }
}