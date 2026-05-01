public static class PathHelper
{
    /// <summary>
    /// Finds the game's data root directory, working correctly for both
    /// WinForms (bin\Debug\net10.0-windows\) and MAUI output layouts.
    /// </summary>
    public static string DataRoot
    {
        get
        {
            // First check: is 'data' right next to the exe? (MAUI, published builds)
            string next = Path.Combine(AppContext.BaseDirectory, "data");
            if (Directory.Exists(next))
            {
                return next;
            }

            // Second check: walk up from exe looking for a 'data' sibling
            // (WinForms dev builds where exe is deep in bin\Debug\...)
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory).Parent;
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            // Fallback: return the next-to-exe path even if it doesn't exist yet
            return next;
        }
    }
}