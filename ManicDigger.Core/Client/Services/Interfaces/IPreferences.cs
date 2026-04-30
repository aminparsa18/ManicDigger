public interface IPreferences
{
    bool GetBool(string key, bool default_);
    int GetInt(string key, int default_);
    string GetString(string key, string default_);
    void SetBool(string key, bool value);
    void SetInt(string key, int value);
    void SetString(string key, string value);
    void SetValues();
    void Remove(string key);
    IEnumerable<string> ToLines();
}